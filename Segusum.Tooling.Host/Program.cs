using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Segusum.Scripting.Core;
using Segusum.Scripting.Semantics;
using Segusum.Scripting.Tooling;

var host = new ToolingHost();
await host.RunAsync();

internal sealed class ToolingHost
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ConcurrentDictionary<int, CancellationTokenSource> requests = new();
    private readonly SemaphoreSlim outputLock = new(1, 1);
    private readonly SemaphoreSlim rpcGate = new(1, 1);
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private MsBuildWorkspaceContext? context;
    private string? projectPath;
    private INamedTypeSymbol? world;
    private INamedTypeSymbol? semanticTarget;
    private DslSemanticWorkspace? semantic;
    private DslSemanticWorkspace? overlaySemantic;
    private string? overlayPath;
    private string? overlayText;
    private INamedTypeSymbol? overlayTarget;
    private readonly object semanticGate = new();
    private IReadOnlyList<DslSource> sources = Array.Empty<DslSource>();
    private Dictionary<string, DslSource> sourcesByPath = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, INamedTypeSymbol?> worldsById = new(StringComparer.Ordinal);
    private Dictionary<string, string?> worldIdBySegPath = new(StringComparer.OrdinalIgnoreCase);
    private DslParseCache parseCache = new();

    public async Task RunAsync()
    {
        var work = new List<Task>();
        string? line;
        while ((line = await Console.In.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            HostRequest? request;
            try { request = JsonSerializer.Deserialize<HostRequest>(line, JsonOptions); }
            catch (Exception ex) { await ErrorAsync(null, "Invalid JSON: " + ex.Message); continue; }
            if (request == null) continue;
            if (request.Method == "cancel") { Cancel(request.Params?.RequestId); continue; }
            work.Add(ProcessAsync(request, Stopwatch.GetTimestamp()));
        }
        await Task.WhenAll(work);
    }

    private async Task ProcessAsync(HostRequest request, long receivedAt)
    {
        using var cts = new CancellationTokenSource();
        requests[request.Id] = cts;
        Console.Error.WriteLine($"RPC start #{request.Id} {request.Method} pending={requests.Count}");
        var enteredExecution = false;
        try
        {
            await rpcGate.WaitAsync(cts.Token).ConfigureAwait(false);
            enteredExecution = true;
            await WriteAsync(new HostResponse(request.Id, null, null, true));
            Console.Error.WriteLine($"RPC execute #{request.Id} {request.Method} queueWait={Stopwatch.GetElapsedTime(receivedAt).TotalMilliseconds:0}ms");
            var result = await ExecuteAsync(request, cts.Token, receivedAt);
            await WriteAsync(new HostResponse(request.Id, result, null));
        }
        catch (OperationCanceledException) { await WriteAsync(new HostResponse(request.Id, null, new HostError("cancelled", "Operation cancelled."))); }
        catch (Exception ex) { await WriteAsync(new HostResponse(request.Id, null, new HostError("host", ex.Message))); }
        finally
        {
            requests.TryRemove(request.Id, out _);
            if (enteredExecution) rpcGate.Release();
            Console.Error.WriteLine($"RPC end #{request.Id} {request.Method} pending={requests.Count}");
        }
    }

    private void Cancel(int? id) { if (id.HasValue && requests.TryGetValue(id.Value, out var cts)) { Console.Error.WriteLine($"RPC cancel #{id.Value} pending={requests.Count}"); cts.Cancel(); } }

    private readonly SemaphoreSlim completionGate = new(1, 1);

    private async Task<object?> ExecuteAsync(HostRequest request, CancellationToken cancellationToken, long receivedAt)
    {
        switch (request.Method)
        {
            case "initialize":
                await InitializeAsync(request.Params?.ProjectPath, cancellationToken);
                return new { projectPath, worlds = worldsById.Where(x => x.Value != null).Select(x => new { id = x.Key, name = x.Value!.ToDisplayString() }).ToArray() };
            case "invalidate":
                if (projectPath != null) await InitializeAsync(projectPath, cancellationToken);
                return new { invalidated = true };
            case "definition":
                return Definition(request.Params, cancellationToken);
            case "references":
                return await ReferencesAsync(request.Params, cancellationToken);
            case "rename":
                return Rename(request.Params, cancellationToken);
            case "completion":
                await completionGate.WaitAsync(cancellationToken);
                try { return Completions(request.Params, cancellationToken, receivedAt); }
                finally { completionGate.Release(); }
            default: throw new InvalidOperationException($"Unknown method '{request.Method}'.");
        }
    }

    private async Task InitializeAsync(string? requestedProject, CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var initializeTimer = Stopwatch.StartNew();
            projectPath = requestedProject ?? DiscoverProject(Environment.CurrentDirectory);
            if (projectPath == null) throw new InvalidOperationException("No .csproj containing .seg files was found.");

            // Drop every object graph rooted in the previous compilation before
            // opening the replacement project. MSBuildWorkspace.Dispose() alone
            // does not clear the host's semantic caches or symbol references.
            var oldContext = context;
            context = null;
            world = null;
            semanticTarget = null;
            semantic = null;
            overlaySemantic = null;
            overlayTarget = null;
            overlayPath = null;
            overlayText = null;
            sources = Array.Empty<DslSource>();
            sourcesByPath = new Dictionary<string, DslSource>(StringComparer.OrdinalIgnoreCase);
            worldsById = new Dictionary<string, INamedTypeSymbol?>(StringComparer.Ordinal);
            worldIdBySegPath = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            parseCache = new DslParseCache();
            oldContext?.Dispose();
            if (oldContext != null)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            var openStarted = initializeTimer.Elapsed;
            var newContext = await MsBuildWorkspaceContext.OpenProjectAsync(projectPath, cancellationToken).ConfigureAwait(false);
            context = newContext;
            var openMs = (initializeTimer.Elapsed - openStarted).TotalMilliseconds;
            Console.Error.WriteLine($"initialize phase=msbuild-project-opened elapsed={initializeTimer.Elapsed.TotalMilliseconds:0}ms project={projectPath}");
            var generatedTrees = context.Compilation.SyntaxTrees
                .Where(x => SegusumGeneratedSource.IsGenerated(x))
                .Select(x => x.FilePath ?? "<generated>")
                .ToArray();
            var mikeGenerated = context.Compilation.SyntaxTrees.Any(x => SegusumGeneratedSource.IsGenerated(x) && x.GetText().ToString().Contains("creaCicloMikeNonRipete", StringComparison.Ordinal));
            var rebuildStarted = initializeTimer.Elapsed;
            await RebuildAsync(cancellationToken);
            Console.Error.WriteLine($"initialize phase=seg-sources-rebuilt elapsed={initializeTimer.Elapsed.TotalMilliseconds:0}ms duration={(initializeTimer.Elapsed - rebuildStarted).TotalMilliseconds:0}ms sources={sources.Count}");
            var worldIndexStarted = initializeTimer.Elapsed;
            BuildWorldIndex(context.Compilation);
            Console.Error.WriteLine($"initialize phase=world-index-built elapsed={initializeTimer.Elapsed.TotalMilliseconds:0}ms duration={(initializeTimer.Elapsed - worldIndexStarted).TotalMilliseconds:0}ms worlds={worldsById.Count}");
            // Sources (including world directives) must be loaded before selecting the
            // default world; otherwise a multi-world project leaves this cache key null.
            var findWorldStarted = initializeTimer.Elapsed;
            world = FindWorld(context.Compilation, null);
            Console.Error.WriteLine($"initialize phase=default-world-selected elapsed={initializeTimer.Elapsed.TotalMilliseconds:0}ms duration={(initializeTimer.Elapsed - findWorldStarted).TotalMilliseconds:0}ms found={(world != null)}");
            Console.Error.WriteLine($"initialize project={projectPath} openProject={openMs:0}ms generatedSegusumTrees={generatedTrees.Length} mikeHelper={mikeGenerated} total={initializeTimer.Elapsed.TotalMilliseconds:0}ms");
            foreach (var tree in generatedTrees) Console.Error.WriteLine($"generatedSegusumTree={tree}");
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    private async Task RebuildAsync(CancellationToken cancellationToken)
    {
        if (context == null) throw new InvalidOperationException("Host is not initialized.");
        sources = Directory.EnumerateFiles(Path.GetDirectoryName(projectPath!)!, "*.seg", SearchOption.AllDirectories)
            .Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.Ordinal).Select(x => new DslSource(x, File.ReadAllText(x))).ToArray();
        sourcesByPath = sources.ToDictionary(x => NormalizePath(x.Path), x => x, StringComparer.OrdinalIgnoreCase);
        semantic = null;
        semanticTarget = null;
        overlaySemantic = null;
        overlayPath = null;
        overlayText = null;
        overlayTarget = null;
        await Task.CompletedTask;
    }

    private DslSemanticWorkspace Workspace(HostParams? parameters, CancellationToken cancellationToken)
    {
        if (context == null) throw new InvalidOperationException("Host is not initialized.");
        lock (semanticGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = FindWorld(context.Compilation, parameters) ?? world ?? throw new InvalidOperationException("No target World was found.");
            if (parameters?.Text is { } overlayTextValue && parameters.Path is { } overlayPathValue)
            {
                var diskSource = sources.FirstOrDefault(x => string.Equals(x.Path, overlayPathValue, StringComparison.OrdinalIgnoreCase));
                if (diskSource == null || !string.Equals(diskSource.Text, overlayTextValue, StringComparison.Ordinal))
                {
                    if (overlaySemantic == null || !string.Equals(overlayPath, overlayPathValue, StringComparison.OrdinalIgnoreCase) ||
                         !string.Equals(overlayText, overlayTextValue, StringComparison.Ordinal) ||
                         !SymbolEqualityComparer.Default.Equals(target, overlayTarget))
                    {
                        var overlayStarted = Stopwatch.StartNew();
                        using var overlayWatchdog = new CancellationTokenSource();
                        var overlayCompleted = false;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(2), overlayWatchdog.Token);
                                if (!overlayCompleted) SaveOverlayDiagnostic(overlayPathValue, overlayTextValue);
                            }
                            catch (OperationCanceledException) { }
                        });
                        Console.Error.WriteLine($"semanticOverlay parse-start path={overlayPathValue} overlay=true chars={overlayTextValue.Length} sha256={HashText(overlayTextValue)}");
                        var overlay = sources.Select(x => string.Equals(x.Path, overlayPathValue, StringComparison.OrdinalIgnoreCase) ? new DslSource(x.Path, overlayTextValue) : x).ToArray();
                        DslSemanticWorkspace candidate;
                        try { candidate = new DslSemanticWorkspace(context, target, overlay, parseCache, overlayPathValue, cancellationToken); }
                        finally { overlayCompleted = true; overlayWatchdog.Cancel(); }
                        cancellationToken.ThrowIfCancellationRequested();
                        overlaySemantic = candidate;
                        overlayPath = overlayPathValue;
                        overlayText = overlayTextValue;
                        overlayTarget = target;
                        Console.Error.WriteLine($"semanticOverlay parse-complete project={projectPath} world={target.ToDisplayString()} elapsed={overlayStarted.Elapsed.TotalMilliseconds:0}ms path={overlayPathValue} textLength={overlayTextValue.Length} sha256={HashText(overlayTextValue)} declarations={candidate.DeclarationCount} diagnostics={candidate.Diagnostics.Count}");
                        return overlaySemantic;
                    }
                    return overlaySemantic!;
                }
            }
            if (semantic == null || !SymbolEqualityComparer.Default.Equals(target, semanticTarget))
            {
                var semanticStarted = Stopwatch.StartNew();
                semantic = new DslSemanticWorkspace(context, target, sources, parseCache, null);
                semanticTarget = target;
                Console.Error.WriteLine($"semanticBuild project={projectPath} world={target.ToDisplayString()} elapsed={semanticStarted.Elapsed.TotalMilliseconds:0}ms sources={sources.Count}");
            }
            return semantic;
        }
    }

    private object? Definition(HostParams? p, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var wasCached = semantic != null || IsOverlayCacheHit(p);
        var result = Workspace(p, ct).GetDefinition(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1);
        stopwatch.Stop();
        Console.Error.WriteLine($"definition project={projectPath} semanticBuild={(wasCached ? 0 : stopwatch.Elapsed.TotalMilliseconds):0}ms lookup={(wasCached ? stopwatch.Elapsed.TotalMilliseconds : 0):0}ms total={stopwatch.Elapsed.TotalMilliseconds:0}ms found={result != null}");
        return result == null ? null : ToDto(result);
    }

    private bool IsOverlayCacheHit(HostParams? parameters)
        => overlaySemantic != null && parameters?.Text != null && parameters.Path != null &&
           string.Equals(overlayPath, parameters.Path, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(overlayText, parameters.Text, StringComparison.Ordinal);

    private async Task<object> ReferencesAsync(HostParams? p, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await Workspace(p, ct).FindReferencesAsync(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1, ct);
        stopwatch.Stop();
        Console.Error.WriteLine($"references project={projectPath} total={stopwatch.Elapsed.TotalMilliseconds:0}ms count={result.Count}");
        return result.Select(ToDto).ToArray();
    }

    private object Rename(HostParams? p, CancellationToken ct)
    {
        var result = Workspace(p, ct).RenameSymbol(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1, p?.NewName ?? "", ct);
        return new { succeeded = result.Succeeded, edits = result.Edits.Select(x => new { path = x.Path, line = x.Span.Line, column = x.Span.Column, start = x.Span.Start, length = x.Span.Length, newText = x.NewText }), diagnostics = result.Diagnostics.Select(x => new { id = x.Id, message = x.Message, path = x.Span.Path, line = x.Span.Line, column = x.Span.Column }) };
    }

    private object Completions(HostParams? p, CancellationToken ct, long receivedAt)
    {
        var total = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();
        var hadSemantic = semantic != null;
        var build = Stopwatch.StartNew();
        var workspace = Workspace(p, ct);
        build.Stop();
        ct.ThrowIfCancellationRequested();
        var query = Stopwatch.StartNew();
        var result = workspace.GetCompletions(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1, p?.Text);
        ct.ThrowIfCancellationRequested();
        query.Stop(); total.Stop();
        var queueWait = Stopwatch.GetElapsedTime(receivedAt).TotalMilliseconds;
        Console.Error.WriteLine($"completion project={projectPath} queueWait={queueWait:0}ms overlay={(p?.Text == null ? 0 : build.Elapsed.TotalMilliseconds):0}ms semanticBuild={(hadSemantic ? 0 : build.Elapsed.TotalMilliseconds):0}ms completionQuery={query.Elapsed.TotalMilliseconds:0}ms total={total.Elapsed.TotalMilliseconds:0}ms");
        return result.Select(x => new { label = x }).ToArray();
    }

    private static object ToDto(SemanticReference x) => new { displayName = x.DisplayName, path = x.Location.Path, line = x.Location.Span.Line, column = x.Location.Span.Column, length = x.Location.Span.Length, language = x.Location.Language, kind = x.Location.Kind };
    private static object ToDto(SemanticDefinition x) => new { displayName = x.DisplayName, path = x.Location.Path, line = x.Location.Span.Line, column = x.Location.Span.Column, length = x.Location.Span.Length, language = x.Location.Language, kind = x.Location.Kind };

    private static string? DiscoverProject(string root)
    {
        var candidates = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).Where(x => Directory.EnumerateFiles(Path.GetDirectoryName(x)!, "*.seg", SearchOption.AllDirectories).Any()).OrderBy(x => x.Length).ToArray();
        return candidates.FirstOrDefault();
    }

    private void BuildWorldIndex(Compilation compilation)
    {
        var timer = Stopwatch.StartNew();
        var attribute = compilation.GetTypeByMetadataName("Seg.SegusumWorldAttribute");
        var indexedWorlds = new Dictionary<string, INamedTypeSymbol?>(StringComparer.Ordinal);
        foreach (var candidate in EnumerateTypes(compilation.Assembly.GlobalNamespace))
        {
            var id = candidate.GetAttributes()
                .Where(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, attribute))
                .Select(x => x.ConstructorArguments.FirstOrDefault().Value as string)
                .FirstOrDefault(x => x != null);
            if (id == null) continue;
            if (!indexedWorlds.TryGetValue(id, out var existing)) indexedWorlds[id] = candidate;
            else if (existing != null && !SymbolEqualityComparer.Default.Equals(existing, candidate)) indexedWorlds[id] = null;
        }

        var sourceWorlds = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var parseTimer = Stopwatch.StartNew();
        foreach (var source in sources)
            sourceWorlds[NormalizePath(source.Path)] = ExtractWorldId(source.Text);
        parseTimer.Stop();
        worldsById = indexedWorlds;
        worldIdBySegPath = sourceWorlds;
        Console.Error.WriteLine($"worldIndex worlds={worldsById.Count} sources={worldIdBySegPath.Count} typeDiscovery={timer.Elapsed.TotalMilliseconds - parseTimer.Elapsed.TotalMilliseconds:0}ms sourceWorldParse={parseTimer.Elapsed.TotalMilliseconds:0}ms total={timer.Elapsed.TotalMilliseconds:0}ms");
    }

    private INamedTypeSymbol? FindWorld(Compilation compilation, HostParams? parameters)
    {
        var total = Stopwatch.StartNew();
        var path = parameters?.Path;
        if (path?.EndsWith(".seg", StringComparison.OrdinalIgnoreCase) == true)
        {
            var lookup = Stopwatch.StartNew();
            var normalizedPath = NormalizePath(path);
            if (overlayTarget != null && string.Equals(overlayPath, path, StringComparison.OrdinalIgnoreCase) &&
                parameters?.Text != null && string.Equals(overlayText, parameters.Text, StringComparison.Ordinal))
            {
                lookup.Stop();
                Console.Error.WriteLine($"worldResolve path={path} sourceLookup={lookup.Elapsed.TotalMilliseconds:0.0}ms directive=<overlay-cache> indexed=true total={total.Elapsed.TotalMilliseconds:0.0}ms found=true");
                return overlayTarget;
            }
            sourcesByPath.TryGetValue(normalizedPath, out var source);
            var id = parameters?.Text != null && (source == null || !string.Equals(source.Text, parameters.Text, StringComparison.Ordinal))
                ? ExtractWorldId(parameters.Text)
                : worldIdBySegPath.GetValueOrDefault(normalizedPath);
            lookup.Stop();
            var result = id != null && worldsById.TryGetValue(id, out var candidate) ? candidate : null;
            result ??= result == null && worldsById.Values.Count(x => x != null) == 1 ? worldsById.Values.First(x => x != null) : null;
            Console.Error.WriteLine($"worldResolve path={path} sourceLookup={lookup.Elapsed.TotalMilliseconds:0}ms directive={id ?? "<none>"} indexed=true total={total.Elapsed.TotalMilliseconds:0}ms found={result != null}");
            return result;
        }

        var candidates = worldsById.Values.Where(x => x != null).Cast<INamedTypeSymbol>().ToArray();
        if (path != null)
        {
            var lookup = Stopwatch.StartNew();
            var normalizedPath = NormalizePath(path);
            var tree = compilation.SyntaxTrees.FirstOrDefault(x => string.Equals(NormalizePath(x.FilePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
            var declared = tree == null ? Array.Empty<INamedTypeSymbol>() : tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Select(x => compilation.GetSemanticModel(tree).GetDeclaredSymbol(x)).OfType<INamedTypeSymbol>().ToArray();
            var inFile = candidates.Where(x => declared.Any(d => SymbolEqualityComparer.Default.Equals(x, d))).ToArray();
            lookup.Stop();
            if (inFile.Length == 1)
            {
                Console.Error.WriteLine($"worldResolve path={path} sourceLookup=0ms directive=<csharp-file> indexed=true total={total.Elapsed.TotalMilliseconds:0}ms found=true");
                return inFile[0];
            }
        }
        var fallback = candidates.Length == 1 ? candidates[0] : null;
        Console.Error.WriteLine($"worldResolve path={path ?? "<none>"} sourceLookup=0ms directive=<fallback> indexed=true total={total.Elapsed.TotalMilliseconds:0}ms found={fallback != null}");
        return fallback;
    }

    private static string NormalizePath(string? path) => path == null ? "" : Path.GetFullPath(path);
    private static string HashText(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)));
    }
    private static void SaveOverlayDiagnostic(string path, string text)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Segusum", "overlay-diagnostics");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Path.GetFileName(path)}");
        File.WriteAllText(file, text);
        Console.Error.WriteLine($"semanticOverlay diagnostic-saved path={file} source={path} chars={text.Length} sha256={HashText(text)}");
    }
    private static string? ExtractWorldId(string text)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;
            if (!trimmed.StartsWith("world", StringComparison.Ordinal) || (trimmed.Length > 5 && !char.IsWhiteSpace(trimmed[5]))) return null;
            var id = trimmed[5..].TrimStart();
            var end = id.IndexOfAny(new[] { ' ', '\t', '\r' });
            return end < 0 ? id : id[..end];
        }
        return null;
    }
    private static string? AttributeId(INamedTypeSymbol type) => type.GetAttributes().Select(x => x.ConstructorArguments.FirstOrDefault().Value as string).FirstOrDefault(x => x != null);
    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns) => ns.GetTypeMembers().Concat(ns.GetNamespaceMembers().SelectMany(EnumerateTypes));
    private async Task WriteAsync(object value) { await outputLock.WaitAsync(); try { Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions)); await Console.Out.FlushAsync(); } finally { outputLock.Release(); } }
    private Task ErrorAsync(int? id, string message) => WriteAsync(new HostResponse(id, null, new HostError("protocol", message)));
}

internal sealed record HostRequest(int Id, string Method, HostParams? Params);
internal sealed record HostParams(string? Path = null, int? Line = null, int? Column = null, string? NewName = null, string? ProjectPath = null, int? RequestId = null, string? Text = null);
internal sealed record HostResponse(int? Id, object? Result, HostError? Error, bool Started = false);
internal sealed record HostError(string Code, string Message);

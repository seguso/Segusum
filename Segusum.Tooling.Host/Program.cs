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
    private MsBuildWorkspaceContext? context;
    private string? projectPath;
    private INamedTypeSymbol? world;
    private INamedTypeSymbol? semanticTarget;
    private DslSemanticWorkspace? semantic;
    private IReadOnlyList<DslSource> sources = Array.Empty<DslSource>();

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
        try
        {
            var result = await ExecuteAsync(request, cts.Token, receivedAt);
            await WriteAsync(new HostResponse(request.Id, result, null));
        }
        catch (OperationCanceledException) { await WriteAsync(new HostResponse(request.Id, null, new HostError("cancelled", "Operation cancelled."))); }
        catch (Exception ex) { await WriteAsync(new HostResponse(request.Id, null, new HostError("host", ex.Message))); }
        finally { requests.TryRemove(request.Id, out _); Console.Error.WriteLine($"RPC end #{request.Id} {request.Method} pending={requests.Count}"); }
    }

    private void Cancel(int? id) { if (id.HasValue && requests.TryGetValue(id.Value, out var cts)) { Console.Error.WriteLine($"RPC cancel #{id.Value} pending={requests.Count}"); cts.Cancel(); } }

    private readonly SemaphoreSlim completionGate = new(1, 1);

    private async Task<object?> ExecuteAsync(HostRequest request, CancellationToken cancellationToken, long receivedAt)
    {
        switch (request.Method)
        {
            case "initialize":
                await InitializeAsync(request.Params?.ProjectPath, cancellationToken);
                return new { projectPath, worlds = EnumerateTypes(context!.Compilation.Assembly.GlobalNamespace).Where(x => x.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Seg.SegusumWorldAttribute")).Select(x => new { id = AttributeId(x), name = x.ToDisplayString() }).ToArray() };
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
        var initializeTimer = Stopwatch.StartNew();
        projectPath = requestedProject ?? DiscoverProject(Environment.CurrentDirectory);
        if (projectPath == null) throw new InvalidOperationException("No .csproj containing .seg files was found.");
        var openStarted = initializeTimer.Elapsed;
        context?.Dispose();
        context = await MsBuildWorkspaceContext.OpenProjectAsync(projectPath, cancellationToken);
        var openMs = (initializeTimer.Elapsed - openStarted).TotalMilliseconds;
        var generatedTrees = context.Compilation.SyntaxTrees
            .Where(x => SegusumGeneratedSource.IsGenerated(x))
            .Select(x => x.FilePath ?? "<generated>")
            .ToArray();
        var mikeGenerated = context.Compilation.SyntaxTrees.Any(x => SegusumGeneratedSource.IsGenerated(x) && x.GetText().ToString().Contains("creaCicloMikeNonRipete", StringComparison.Ordinal));
        await RebuildAsync(cancellationToken);
        // Sources (including world directives) must be loaded before selecting the
        // default world; otherwise a multi-world project leaves this cache key null.
        world = FindWorld(context.Compilation, null);
        Console.Error.WriteLine($"initialize project={projectPath} openProject={openMs:0}ms generatedSegusumTrees={generatedTrees.Length} mikeHelper={mikeGenerated} total={initializeTimer.Elapsed.TotalMilliseconds:0}ms");
        foreach (var tree in generatedTrees) Console.Error.WriteLine($"generatedSegusumTree={tree}");
    }

    private async Task RebuildAsync(CancellationToken cancellationToken)
    {
        if (context == null) throw new InvalidOperationException("Host is not initialized.");
        sources = Directory.EnumerateFiles(Path.GetDirectoryName(projectPath!)!, "*.seg", SearchOption.AllDirectories)
            .Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.Ordinal).Select(x => new DslSource(x, File.ReadAllText(x))).ToArray();
        semantic = null;
        semanticTarget = null;
        await Task.CompletedTask;
    }

    private DslSemanticWorkspace Workspace(HostParams? parameters, CancellationToken cancellationToken)
    {
        if (context == null) throw new InvalidOperationException("Host is not initialized.");
        var target = FindWorld(context.Compilation, parameters?.Path) ?? world ?? throw new InvalidOperationException("No target World was found.");
        if (parameters?.Text != null && parameters.Path != null)
        {
            var overlay = sources.Select(x => string.Equals(x.Path, parameters.Path, StringComparison.OrdinalIgnoreCase) ? new DslSource(x.Path, parameters.Text) : x).ToArray();
            return new DslSemanticWorkspace(context, target, overlay);
        }
        if (semantic == null || !SymbolEqualityComparer.Default.Equals(target, semanticTarget))
        {
            var semanticStarted = Stopwatch.StartNew();
            semantic = new DslSemanticWorkspace(context, target, sources);
            semanticTarget = target;
            Console.Error.WriteLine($"semanticBuild project={projectPath} world={target.ToDisplayString()} elapsed={semanticStarted.Elapsed.TotalMilliseconds:0}ms sources={sources.Count}");
        }
        return semantic;
    }

    private object? Definition(HostParams? p, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var wasCached = semantic != null;
        var result = Workspace(p is null ? null : p with { Text = null }, ct).GetDefinition(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1);
        stopwatch.Stop();
        Console.Error.WriteLine($"definition project={projectPath} semanticBuild={(wasCached ? 0 : stopwatch.Elapsed.TotalMilliseconds):0}ms lookup={(wasCached ? stopwatch.Elapsed.TotalMilliseconds : 0):0}ms total={stopwatch.Elapsed.TotalMilliseconds:0}ms found={result != null}");
        return result == null ? null : ToDto(result);
    }

    private async Task<object> ReferencesAsync(HostParams? p, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await Workspace(p is null ? null : p with { Text = null }, ct).FindReferencesAsync(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1, ct);
        stopwatch.Stop();
        Console.Error.WriteLine($"references project={projectPath} total={stopwatch.Elapsed.TotalMilliseconds:0}ms count={result.Count}");
        return result.Select(ToDto).ToArray();
    }

    private object Rename(HostParams? p, CancellationToken ct)
    {
        var result = Workspace(p, ct).RenameSymbol(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1, p?.NewName ?? "");
        return new { succeeded = result.Succeeded, edits = result.Edits.Select(x => new { path = x.Path, line = x.Span.Line, column = x.Span.Column, start = x.Span.Start, length = x.Span.Length, newText = x.NewText }), diagnostics = result.Diagnostics.Select(x => new { id = x.Id, message = x.Message, path = x.Span.Path, line = x.Span.Line, column = x.Span.Column }) };
    }

    private object Completions(HostParams? p, CancellationToken ct, long receivedAt)
    {
        var total = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();
        var hadSemantic = semantic != null;
        var build = Stopwatch.StartNew();
        var stable = p == null ? null : p with { Text = null };
        var workspace = Workspace(stable, ct);
        build.Stop();
        ct.ThrowIfCancellationRequested();
        var query = Stopwatch.StartNew();
        var result = workspace.GetCompletions(p?.Path ?? "", p?.Line ?? 1, p?.Column ?? 1, p?.Text);
        ct.ThrowIfCancellationRequested();
        query.Stop(); total.Stop();
        var queueWait = Stopwatch.GetElapsedTime(receivedAt).TotalMilliseconds;
        Console.Error.WriteLine($"completion project={projectPath} queueWait={queueWait:0}ms overlay=0ms semanticBuild={(hadSemantic ? 0 : build.Elapsed.TotalMilliseconds):0}ms completionQuery={query.Elapsed.TotalMilliseconds:0}ms total={total.Elapsed.TotalMilliseconds:0}ms");
        return result.Select(x => new { label = x }).ToArray();
    }

    private static object ToDto(SemanticReference x) => new { displayName = x.DisplayName, path = x.Location.Path, line = x.Location.Span.Line, column = x.Location.Span.Column, length = x.Location.Span.Length, language = x.Location.Language, kind = x.Location.Kind };
    private static object ToDto(SemanticDefinition x) => new { displayName = x.DisplayName, path = x.Location.Path, line = x.Location.Span.Line, column = x.Location.Span.Column, length = x.Location.Span.Length, language = x.Location.Language, kind = x.Location.Kind };

    private static string? DiscoverProject(string root)
    {
        var candidates = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).Where(x => Directory.EnumerateFiles(Path.GetDirectoryName(x)!, "*.seg", SearchOption.AllDirectories).Any()).OrderBy(x => x.Length).ToArray();
        return candidates.FirstOrDefault();
    }

    private INamedTypeSymbol? FindWorld(Compilation compilation, string? path)
    {
        var attribute = compilation.GetTypeByMetadataName("Seg.SegusumWorldAttribute");
        var candidates = EnumerateTypes(compilation.Assembly.GlobalNamespace).Where(x => x.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute))).ToArray();
        if (path?.EndsWith(".seg", StringComparison.OrdinalIgnoreCase) == true)
        {
            var source = sources.FirstOrDefault(x => string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
            var id = source == null ? null : DslParser.Parse(source).Document.WorldId;
            if (id != null) candidates = candidates.Where(x => AttributeId(x) == id).ToArray();
        }
        else if (path != null)
        {
            var tree = compilation.SyntaxTrees.FirstOrDefault(x => string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
            var declared = tree == null ? Array.Empty<INamedTypeSymbol>() : tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Select(x => compilation.GetSemanticModel(tree).GetDeclaredSymbol(x)).OfType<INamedTypeSymbol>().ToArray();
            var inFile = candidates.Where(x => declared.Any(d => SymbolEqualityComparer.Default.Equals(x, d))).ToArray();
            if (inFile.Length == 1) return inFile[0];
        }
        return candidates.Length == 1 ? candidates[0] : null;
    }
    private static string? AttributeId(INamedTypeSymbol type) => type.GetAttributes().Select(x => x.ConstructorArguments.FirstOrDefault().Value as string).FirstOrDefault(x => x != null);
    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns) => ns.GetTypeMembers().Concat(ns.GetNamespaceMembers().SelectMany(EnumerateTypes));
    private async Task WriteAsync(object value) { await outputLock.WaitAsync(); try { Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions)); await Console.Out.FlushAsync(); } finally { outputLock.Release(); } }
    private Task ErrorAsync(int? id, string message) => WriteAsync(new HostResponse(id, null, new HostError("protocol", message)));
}

internal sealed record HostRequest(int Id, string Method, HostParams? Params);
internal sealed record HostParams(string? Path = null, int? Line = null, int? Column = null, string? NewName = null, string? ProjectPath = null, int? RequestId = null, string? Text = null);
internal sealed record HostResponse(int? Id, object? Result, HostError? Error);
internal sealed record HostError(string Code, string Message);

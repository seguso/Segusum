using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Segusum.Scripting.Semantics;

namespace Segusum.Scripting.Tooling;

public interface ICSharpWorkspaceContext
{
    Compilation Compilation { get; }
    Solution Solution { get; }
    CSharpSemanticIndexCache SemanticIndexes { get; }
}

public sealed class AdhocCSharpWorkspaceContext : ICSharpWorkspaceContext
{
    public Compilation Compilation { get; }
    public Solution Solution { get; }
    public CSharpSemanticIndexCache SemanticIndexes { get; }

    public AdhocCSharpWorkspaceContext(Compilation compilation)
    {
        Compilation = compilation;
        SemanticIndexes = new CSharpSemanticIndexCache(compilation);
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("Segusum.Tooling");
        workspace.AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Segusum.Tooling", "Segusum.Tooling", LanguageNames.CSharp,
            compilationOptions: compilation.Options, metadataReferences: compilation.References.OfType<PortableExecutableReference>()));
        foreach (var tree in compilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId, tree.FilePath);
            var text = TextAndVersion.Create(tree.GetText(), VersionStamp.Create(), tree.FilePath);
            workspace.AddDocument(DocumentInfo.Create(documentId, Path.GetFileName(tree.FilePath), filePath: tree.FilePath, loader: TextLoader.From(text)));
        }
        Solution = workspace.CurrentSolution;
    }
}

public sealed class MsBuildWorkspaceContext : ICSharpWorkspaceContext, IDisposable
{
    private readonly MSBuildWorkspace workspace;
    public Compilation Compilation { get; }
    public Solution Solution => workspace.CurrentSolution;

    private MsBuildWorkspaceContext(MSBuildWorkspace workspace, Compilation compilation)
    {
        this.workspace = workspace;
        Compilation = compilation;
        SemanticIndexes = new CSharpSemanticIndexCache(compilation);
    }
    public CSharpSemanticIndexCache SemanticIndexes { get; }

    public static async Task<MsBuildWorkspaceContext> OpenProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var workspace = MSBuildWorkspace.Create();
        LogMemory("msbuild-workspace-created", projectPath);
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        LogMemory("msbuild-project-opened", projectPath);
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not create a compilation for '{projectPath}'.");
        LogMemory("msbuild-compilation-created", projectPath);
        return new MsBuildWorkspaceContext(workspace, compilation);
    }

    public static async Task<MsBuildWorkspaceContext> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        var project = solution.Projects.FirstOrDefault()
            ?? throw new InvalidOperationException($"Solution '{solutionPath}' contains no projects.");
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not create a compilation for '{project.FilePath}'.");
        return new MsBuildWorkspaceContext(workspace, compilation);
    }

    public void Dispose() => workspace.Dispose();

    private static void LogMemory(string phase, string projectPath)
    {
        using var process = Process.GetCurrentProcess();
        Console.Error.WriteLine($"workspaceMemory phase={phase} project={projectPath} workingSetMB={process.WorkingSet64 / 1024 / 1024} privateMB={process.PrivateMemorySize64 / 1024 / 1024} managedMB={GC.GetTotalMemory(false) / 1024 / 1024}");
    }
}

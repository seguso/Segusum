using Segusum.Translator.Core;

namespace Segusum.Translator.Web;

public sealed class TranslatorWebService
{
    public TranslatorWebService(string? initialRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(initialRoot)) ConfigureRoot(initialRoot);
    }
    public string RepositoryRoot { get; private set; } = "";
    public TranslationWorkspace? Workspace { get; private set; }
    public IReadOnlyList<TranslationCatalogInfo> Catalogs { get; private set; } = Array.Empty<TranslationCatalogInfo>();

    public void ConfigureRoot(string root)
    {
        RepositoryRoot = Path.GetFullPath(root);
        Catalogs = TranslationCatalogFile.Discover(RepositoryRoot);
    }

    public void Open(string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(RepositoryRoot)) throw new InvalidOperationException("Indica prima la root del progetto gioco.");
        var workspace = new TranslationWorkspace { RepositoryRoot = RepositoryRoot, CatalogPath = catalogPath };
        workspace.Load();
        workspace.Save();
        Workspace = workspace;
    }

    public void Refresh()
    {
        if (Workspace is null) return;
        Workspace.Load();
        Workspace.Save();
    }
    public void Save() => Workspace?.Save();
}

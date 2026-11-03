namespace AlbionMarket.Core;

/// <summary>
/// Central place for every path the app uses. Everything lives under
/// <c>Documents\Albert\OpenCode\Albion Data Project</c> in three sibling folders
/// so the application, the packet-capture client and the database stay together
/// but separate.
/// </summary>
public static class AppPaths
{
    public const string ProductFolder = "Albion Data Project";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Albert",
        "OpenCode",
        ProductFolder);

    /// <summary>SQLite database (one file, inspectable with any SQLite tool).</summary>
    public static string DataDir => Path.Combine(Root, "Dades");

    /// <summary>Fork of albiondata-client, sources and compiled binary.</summary>
    public static string ClientDir => Path.Combine(Root, "Client");

    /// <summary>MkDocs sources for the project documentation.</summary>
    public static string DocsDir => Path.Combine(Root, "docs");

    public static string DatabaseFile => Path.Combine(DataDir, "market.db");

    /// <summary>
    /// Downloaded item icons. Disposable cache kept inside the project so it is
    /// easy to find and move together with the rest of this personal tool.
    /// </summary>
    public static string IconCacheDir => Path.Combine(Root, "Temp", "icons");

    public static string ForkedClientExe => Path.Combine(ClientDir, "albiondata-client.exe");

    /// <summary>
    /// Picks the binary to launch: the configured path if it exists, otherwise
    /// the fork that ships with the project.
    /// </summary>
    public static string ResolveClientExe(string? configured = null)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        return ForkedClientExe;
    }

    /// <summary>
    /// OneDrive + SQLite WAL is a bad combination: the sync engine can upload
    /// the -wal/-shm files mid-transaction. We switch journaling off for that
    /// case and tell the user.
    /// </summary>
    public static bool IsUnderCloudSync(string path) =>
        path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Dropbox", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Google Drive", StringComparison.OrdinalIgnoreCase);
}

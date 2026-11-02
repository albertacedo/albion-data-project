namespace AlbionMarket.Core;

/// <summary>
/// Central place for every path the app uses. Everything lives under
/// <c>Documents\Albert\Albion Data Client</c> in three sibling folders so the
/// executable, the packet-capture client and the database stay together but
/// separate.
/// </summary>
public static class AppPaths
{
    public const string ProductFolder = "Albion Data Client";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Albert",
        ProductFolder);

    /// <summary>SQLite database (one file, inspectable with any SQLite tool).</summary>
    public static string DataDir => Path.Combine(Root, "Dades");

    /// <summary>Fork of albiondata-client, sources and compiled binary.</summary>
    public static string ClientDir => Path.Combine(Root, "Client");

    public static string DatabaseFile => Path.Combine(DataDir, "market.db");

    public static string ForkedClientExe => Path.Combine(ClientDir, "albiondata-client.exe");

    public static string InstalledClientExe =>
        @"C:\Program Files\Albion Data Client\albiondata-client.exe";

    /// <summary>
    /// Picks the binary to launch: the configured path if it exists, then the
    /// fork inside our own folder, then the official installation.
    /// </summary>
    public static string ResolveClientExe(string? configured = null)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        if (File.Exists(ForkedClientExe))
            return ForkedClientExe;

        if (File.Exists(InstalledClientExe))
            return InstalledClientExe;

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

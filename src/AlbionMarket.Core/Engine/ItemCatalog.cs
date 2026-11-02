using System.Text;
using AlbionMarket.Core.Data;

namespace AlbionMarket.Core.Engine;

/// <summary>
/// Downloads the community item dump (id -> localized name) so the UI can show
/// "Adept's Bag" instead of "T4_BAG". Only runs when the table is empty.
/// </summary>
public static class ItemCatalog
{
    private const string ItemsUrl =
        "https://raw.githubusercontent.com/ao-data/ao-bin-dumps/master/formatted/items.txt";

    public static async Task<int> SyncAsync(
        MarketDatabase db, Action<string>? log = null, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AlbionMarket/0.1");

        log?.Invoke("Descarregant el catàleg d'items...");
        var text = await http.GetStringAsync(ItemsUrl, ct).ConfigureAwait(false);

        var rows = new List<(string, string)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;

            // Format: "   1: T4_BAG                              : Adept's Bag"
            var parts = line.Split(':', 3);
            if (parts.Length < 3) continue;

            var id = parts[1].Trim();
            var name = parts[2].Trim();
            if (id.Length == 0 || name.Length == 0) continue;
            if (!id.StartsWith("T", StringComparison.OrdinalIgnoreCase) &&
                !id.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase)) continue;

            rows.Add((id, name));
        }

        if (rows.Count == 0)
        {
            log?.Invoke("El catàleg ha arribat buit; es manté l'antic.");
            return 0;
        }

        await db.SaveItemsAsync(rows, ct).ConfigureAwait(false);
        log?.Invoke($"Catàleg actualitzat: {rows.Count} items.");
        return rows.Count;
    }
}

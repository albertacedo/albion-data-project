using AlbionMarket.Core;
using AlbionMarket.Core.Engine;
using AlbionMarket.Core.Models;

// Headless harness for the core: lets you verify the API, the flip maths and
// the local ingest pipeline without opening the WPF window.
//
//   dotnet run --project src/AlbionMarket.Cli -- flips
//   dotnet run --project src/AlbionMarket.Cli -- fetch T4_BAG T5_BAG
//   dotnet run --project src/AlbionMarket.Cli -- serve 5099

var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "flips";
var rest = args.Skip(1).ToArray();

await using var svc = new MarketService();
svc.Log += m => Console.WriteLine($"  · {m}");
svc.OrderReceived += o =>
    Console.WriteLine($"  · {o.City,-14} {o.ItemId,-22} {(o.IsOffer ? "VENDA" : "COMPRA"),-7} {o.Price,10:N0} x{o.Amount}");

await svc.StartAsync();

switch (cmd)
{
    case "fetch":
    {
        var items = rest.Length > 0 ? rest.ToList() : svc.Watchlist.ToList();
        svc.Watchlist.Clear();
        svc.Watchlist.AddRange(items);
        await svc.RefreshWatchlistAsync();
        Console.WriteLine();
        await svc.RecomputeFlipsAsync();
        break;
    }

    case "flips":
    {
        if (rest.Length > 0) svc.Watchlist.Clear();
        if (rest.Length > 0) svc.Watchlist.AddRange(rest);
        await svc.RefreshWatchlistAsync();
        Console.WriteLine();
        var flips = await svc.RecomputeFlipsAsync();
        PrintFlips(flips, svc.Options.TaxRate);
        break;
    }

    case "show":
    {
        var flips = await svc.RecomputeFlipsAsync();
        PrintFlips(flips, svc.Options.TaxRate);
        break;
    }

    case "nats":
    {
        var printed = 0;
        svc.StartNats();
        svc.Nats!.Message += (subject, json) =>
        {
            if (Interlocked.Increment(ref printed) <= 2)
                Console.WriteLine($"[{subject}] {json[..Math.Min(700, json.Length)]}");
        };
        Console.WriteLine("Subscrit a marketorders.deduped. Escoltant 20 segons...");
        await Task.Delay(TimeSpan.FromSeconds(20));
        Console.WriteLine($"Missatges NATS: {svc.Nats?.MessagesReceived ?? 0} · ordres a la BD: {await svc.Db.CountOrdersAsync()}");
        break;
    }

    case "serve":
    {
        if (rest.Length > 0 && int.TryParse(rest[0], out var _)) { /* port fixed at 5099 by design */ }

        Console.WriteLine($"Ingesta escoltant a http://localhost:{svc.IngestPort}/marketorders.ingest");
        Console.WriteLine("Envia-li un JSON amb la forma: {{\"Orders\":[ ... ]}} (vegeu el README).");
        Console.WriteLine("Ctrl+C per sortir.");
        await Task.Delay(Timeout.Infinite);
        break;
    }

    case "items":
    {
        var names = await svc.Db.GetItemNamesAsync();
        Console.WriteLine($"Catàleg: {names.Count} items a la BD");
        var q = rest.Length > 0 ? string.Join(" ", rest) : "";
        foreach (var (id, name) in names
                     .Where(kv => kv.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                  kv.Value.Contains(q, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(kv => kv.Key)
                     .Take(40))
            Console.WriteLine($"{id,-32} {name}");
        break;
    }

    case "catalog":
    {
        var n = await ItemCatalog.SyncAsync(svc.Db, m => Console.WriteLine($"  · {m}"));
        Console.WriteLine($"Resultat: {n} items desats (total a la BD: {await svc.Db.CountItemsAsync()})");
        break;
    }

    case "selftest":
    {
        // 1) valid item from the API
        svc.Watchlist.Clear();
        svc.Watchlist.Add("T4_BAG");
        await svc.RefreshWatchlistAsync();
        var apiRows = await svc.Db.GetPricesAsync("T4_BAG");
        Console.WriteLine($"API -> {apiRows.Count} files per T4_BAG");

        // 2) simulate the sniffer: two cities with a fat spread
        var now = DateTime.UtcNow;
        var orderPayload = """
        {"Orders":[
          {"Id":900000001,"ItemTypeId":"T4_BAG","ItemGroupTypeId":"bag","LocationId":"3008","QualityLevel":1,"EnchantmentLevel":0,"UnitPriceSilver":1000,"Amount":10,"AuctionType":"offer","Expires":"2026-12-01T00:00:00"},
          {"Id":900000002,"ItemTypeId":"T4_BAG","ItemGroupTypeId":"bag","LocationId":"3005","QualityLevel":1,"EnchantmentLevel":0,"UnitPriceSilver":5000,"Amount":4,"AuctionType":"request","Expires":"2026-12-01T00:00:00"}
        ]}
        """;
        await svc.Ingest.Handler!("marketorders.ingest", orderPayload);
        var merged = await svc.Db.GetPricesAsync("T4_BAG");
        Console.WriteLine($"Després del sniffer -> {merged.Count} files per T4_BAG");
        foreach (var p in merged.OrderBy(p => p.City))
            Console.WriteLine($"  {p.City,-14} sellMin={p.SellPriceMin,8:N0} buyMax={p.BuyPriceMax,8:N0} src={p.Source}");

        var flips = await svc.RecomputeFlipsAsync();
        var bag = flips.FirstOrDefault(f => f.ItemId == "T4_BAG");
        Console.WriteLine(bag is null ? "SELFTEST: cap flip per T4_BAG" : $"SELFTEST OK -> compra {bag.BuyCity} {bag.BuyPrice:N0} / ven {bag.SellCity} {bag.InstantSellPrice:N0} = +{bag.InstantNetProfit:N0}");
        break;
    }

    default:
        Console.WriteLine("Comandes: flips | show | fetch [items...] | serve | items [text] | catalog | selftest");
        break;
}

static void PrintFlips(List<Flip> flips, double tax)
{
    Console.WriteLine();
    Console.WriteLine(string.Format("{0,-20} {1,-13} {2,9} {3,-13} {4,9} {5,9} {6,7} {7,9} {8,7} {9,5}",
        "ITEM", "COMPRA A", "PAGUES", "VEN A", "INSTANT", "NET", "ROI", "NET LIST", "ROI L", "EDAT"));
    Console.WriteLine(new string('-', 100));
    foreach (var f in flips.Take(40))
    {
        Console.WriteLine($"{f.ItemId,-20} {f.BuyCity,-13} {f.BuyPrice,9:N0} {f.SellCity,-13} {f.InstantSellPrice,9:N0} " +
                          $"{f.InstantNetProfit,9:N0} {f.InstantRoiPercent,6:0.0}% {f.ListedNetProfit,9:N0} {f.ListedRoiPercent,6:0.0}% {f.WorstAgeHours,4:0}h");
    }
    if (flips.Count == 0) Console.WriteLine("(cap oportunitat amb els filtres actuals)");
    Console.WriteLine($"\n{flips.Count} oportunitats · taxa {tax:P0} · dades de l'API i del sniffer local");
}

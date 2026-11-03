using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AlbionMarket.Core;

namespace AlbionMarket.App;

/// <summary>
/// Item icons from the community render service
/// (<c>render.albiononline.com/v1/item/{id}?quality={q}&amp;size=96</c>).
///
/// Rows ask for their icon only when the grid realises them (see the
/// <c>LoadingRow</c> handlers), so a long list costs nothing until it is
/// scrolled. Downloads run four at a time on the thread pool: WPF's
/// <c>IsAsync</c> binding would queue them one by one and a slow response would
/// stall every icon behind it.
///
/// The service is not always healthy (timeouts and 502s happen), so a failure
/// only blocks retries for a couple of minutes instead of the whole session; a
/// 404 is treated as final because the item really has no render.
/// </summary>
public static class ItemIcons
{
    private const string BaseUrl = "https://render.albiononline.com/v1/item/";
    private const int DecodeWidth = 96;
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(2);

    private static readonly ConcurrentDictionary<string, ImageSource> Hits = new();
    private static readonly ConcurrentDictionary<string, DateTime> NotBefore = new();
    private static readonly ConcurrentDictionary<string, object> Gates = new();
    private static readonly SemaphoreSlim Slots = new(4, 4);

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AlbionMarket/0.1 (personal tool)");
        return http;
    }

    /// <summary>
    /// Starts loading the icon and calls <paramref name="onLoaded"/> on the UI
    /// thread when it is ready. A cached icon is delivered synchronously.
    /// </summary>
    public static void LoadAsync(string itemId, int quality, Action<ImageSource> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;

        var key = $"{itemId}_{quality}";
        if (Hits.TryGetValue(key, out var hit))
        {
            onLoaded(hit);
            return;
        }
        if (NotBefore.TryGetValue(key, out var notBefore) && DateTime.UtcNow < notBefore) return;

        _ = Task.Run(async () =>
        {
            await Slots.WaitAsync().ConfigureAwait(false);
            try
            {
                var image = Get(itemId, quality);
                if (image is null) return;

                var dispatcher = Application.Current?.Dispatcher;
                dispatcher?.BeginInvoke(() => onLoaded(image));
            }
            finally
            {
                Slots.Release();
            }
        });
    }

    private static ImageSource? Get(string itemId, int quality)
    {
        var key = $"{itemId}_{quality}";
        if (Hits.TryGetValue(key, out var hit)) return hit;

        // Two rows for the same item can race; one download per key is enough.
        var gate = Gates.GetOrAdd(key, _ => new object());
        lock (gate)
        {
            if (Hits.TryGetValue(key, out hit)) return hit;

            var image = Load(key, itemId, quality);
            if (image is null)
            {
                NotBefore[key] = DateTime.UtcNow + RetryAfter;
                return null;
            }

            Hits[key] = image;
            NotBefore.TryRemove(key, out _);
            return image;
        }
    }

    private static ImageSource? Load(string key, string itemId, int quality)
    {
        var path = Path.Combine(AppPaths.IconCacheDir, key + "-96.png");

        byte[]? bytes = null;
        try
        {
            if (File.Exists(path)) bytes = File.ReadAllBytes(path);
        }
        catch { /* caché corrupta: es torna a baixar */ }

        if (bytes is null)
        {
            try
            {
                var url = $"{BaseUrl}{Uri.EscapeDataString(itemId)}?quality={quality}&size={DecodeWidth}";
                bytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();

                // Best effort: if the cache cannot be written the icon still shows.
                try
                {
                    Directory.CreateDirectory(AppPaths.IconCacheDir);
                    File.WriteAllBytes(path, bytes);
                }
                catch { }
            }
            catch (Exception ex)
            {
                // A 404 means "this item has no render": not worth retrying soon.
                if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
                    NotBefore[key] = DateTime.UtcNow + TimeSpan.FromHours(1);

                return null;
            }
        }

        try
        {
            return Decode(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource Decode(byte[] bytes)
    {
        var image = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = DecodeWidth;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze(); // handed over from a pool thread
        return image;
    }
}

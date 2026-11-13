using Litaro.Models;

namespace Litaro.Services;

public class WebContentCache
{
    private readonly object sync = new();
    private readonly Dictionary<string, IReadOnlyList<WebContent>> pages = new(StringComparer.Ordinal);
    private long version;

    public long Version
    {
        get
        {
            lock (sync)
                return version;
        }
    }

    public IReadOnlyList<WebContent>? Get(string pageName)
    {
        lock (sync)
            return pages.TryGetValue(pageName, out var contents) ? contents : null;
    }

    public void Set(string pageName, IReadOnlyList<WebContent> contents, long expectedVersion)
    {
        lock (sync)
        {
            if (version == expectedVersion)
                pages[pageName] = contents;
        }
    }

    public void Invalidate()
    {
        lock (sync)
        {
            version++;
            pages.Clear();
        }
    }
}

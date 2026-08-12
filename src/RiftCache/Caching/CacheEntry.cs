namespace RiftCache.Caching;

public sealed class CacheEntry
{
    private long _lastAccessedTicks;

    public CacheEntry(byte[] value, DateTimeOffset? absoluteExpiration, TimeSpan? slidingExpiration, DateTimeOffset? lastAccessed = null)
    {
        Value = value;
        AbsoluteExpiration = absoluteExpiration;
        SlidingExpiration = slidingExpiration;
        _lastAccessedTicks = (lastAccessed ?? DateTimeOffset.UtcNow).UtcTicks;
    }

    public byte[] Value { get; }

    public DateTimeOffset? AbsoluteExpiration { get; }

    public TimeSpan? SlidingExpiration { get; }

    public DateTimeOffset LastAccessed => new(Interlocked.Read(ref _lastAccessedTicks), TimeSpan.Zero);

    public void Touch(DateTimeOffset? now = null) =>
        Interlocked.Exchange(ref _lastAccessedTicks, (now ?? DateTimeOffset.UtcNow).UtcTicks);

    public bool IsExpired(DateTimeOffset now)
    {
        if (AbsoluteExpiration is { } absolute && now >= absolute)
        {
            return true;
        }

        if (SlidingExpiration is { } sliding && now >= LastAccessed + sliding)
        {
            return true;
        }

        return false;
    }
}

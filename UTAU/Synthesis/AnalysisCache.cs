namespace UTAU.Synthesis;

internal sealed class AnalysisCache(long budgetBytes)
{
    readonly record struct Key(string Path, long WriteTimeTicks, int StartSample, int EndSample, F0Estimator Estimator);

    readonly Lock gate = new();
    readonly Dictionary<Key, LinkedListNode<(Key Key, WorldFeatures Features)>> entries = [];
    readonly LinkedList<(Key Key, WorldFeatures Features)> order = new();
    long usedBytes;

    public static AnalysisCache Shared { get; } = new(DefaultBudgetBytes);

    public const long DefaultBudgetBytes = 512L * 1024 * 1024;

    public long BudgetBytes { get; set; } = budgetBytes;

    public long UsedBytes
    {
        get
        {
            using (gate.EnterScope())
                return usedBytes;
        }
    }

    public int Count
    {
        get
        {
            using (gate.EnterScope())
                return entries.Count;
        }
    }

    public WorldFeatures GetOrAdd(
        string path,
        long writeTimeTicks,
        int startSample,
        int endSample,
        F0Estimator estimator,
        Func<WorldFeatures> factory)
    {
        var key = new Key(path, writeTimeTicks, startSample, endSample, estimator);

        using (gate.EnterScope())
        {
            if (entries.TryGetValue(key, out var existing))
            {
                order.Remove(existing);
                order.AddFirst(existing);
                return existing.Value.Features;
            }
        }

        var features = factory();

        using (gate.EnterScope())
        {
            if (entries.TryGetValue(key, out var raced))
            {
                order.Remove(raced);
                order.AddFirst(raced);
                return raced.Value.Features;
            }

            var node = order.AddFirst((key, features));
            entries[key] = node;
            usedBytes += features.EstimatedBytes;
            Trim();
        }

        return features;
    }

    public void Clear()
    {
        using (gate.EnterScope())
        {
            entries.Clear();
            order.Clear();
            usedBytes = 0;
        }
    }

    void Trim()
    {
        while (order.Count > 1 && usedBytes > BudgetBytes)
        {
            var last = order.Last;
            if (last is null)
                return;

            order.RemoveLast();
            entries.Remove(last.Value.Key);
            usedBytes -= last.Value.Features.EstimatedBytes;
        }
    }
}

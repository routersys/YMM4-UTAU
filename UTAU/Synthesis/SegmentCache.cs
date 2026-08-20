namespace UTAU.Synthesis;

internal sealed class SegmentCache(long budgetBytes)
{
    readonly Lock gate = new();
    readonly Dictionary<SegmentKey, LinkedListNode<(SegmentKey Key, double[] Samples)>> entries = [];
    readonly LinkedList<(SegmentKey Key, double[] Samples)> order = new();
    long usedBytes;

    public static SegmentCache Shared { get; } = new(DefaultBudgetBytes);

    public const long DefaultBudgetBytes = 256L * 1024 * 1024;

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

    public bool TryCopyInto(SegmentKey key, Span<double> destination)
    {
        using (gate.EnterScope())
        {
            if (!entries.TryGetValue(key, out var node))
                return false;
            if (node.Value.Samples.Length != destination.Length)
                return false;

            order.Remove(node);
            order.AddFirst(node);
            node.Value.Samples.CopyTo(destination);
            return true;
        }
    }

    public void Store(SegmentKey key, ReadOnlySpan<double> samples)
    {
        var copy = samples.ToArray();

        using (gate.EnterScope())
        {
            if (entries.ContainsKey(key))
                return;

            var node = order.AddFirst((key, copy));
            entries[key] = node;
            usedBytes += (long)copy.Length * sizeof(double);
            Trim();
        }
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
            usedBytes -= (long)last.Value.Samples.Length * sizeof(double);
        }
    }
}

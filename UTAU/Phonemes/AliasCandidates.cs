namespace UTAU.Phonemes;

internal ref struct AliasCandidates
{
    public const int FormCount = 6;
    public const int StepCount = 14;
    public const int BoundsLength = (FormCount + StepCount) * 2;

    const byte PreviousVowelStage = 0;
    const byte WildcardStage = 1;
    const byte PlainStage = 2;

    static readonly (byte Stage, byte Form)[] Steps =
    [
        (PreviousVowelStage, 0), (PreviousVowelStage, 1), (PreviousVowelStage, 2),
        (PreviousVowelStage, 3), (PreviousVowelStage, 4), (PreviousVowelStage, 5),
        (WildcardStage, 0), (WildcardStage, 3),
        (PlainStage, 0), (PlainStage, 1), (PlainStage, 2),
        (PlainStage, 3), (PlainStage, 4), (PlainStage, 5),
    ];

    readonly Span<char> pool;
    readonly Span<int> starts;
    readonly Span<int> lengths;
    readonly Span<char> emitted;
    readonly Span<int> emittedStarts;
    readonly Span<int> emittedLengths;
    readonly ReadOnlySpan<char> previousVowel;
    readonly bool prefixed;
    readonly bool wildcard;
    int step;
    int emittedCount;
    int written;

    public AliasCandidates(
        ReadOnlySpan<char> lyric,
        ReadOnlySpan<char> previousVowel,
        Span<char> pool,
        Span<int> bounds,
        Span<char> scratch,
        Span<char> emitted)
    {
        this.pool = pool;
        this.previousVowel = previousVowel;
        this.emitted = emitted;
        starts = bounds[..FormCount];
        lengths = bounds.Slice(FormCount, FormCount);
        emittedStarts = bounds.Slice(FormCount * 2, StepCount);
        emittedLengths = bounds.Slice(FormCount * 2 + StepCount, StepCount);
        prefixed = previousVowel.Length > 0;
        wildcard = prefixed && !previousVowel.SequenceEqual(KanaRomanization.StartVowel);
        BuildForms(lyric, pool, starts, lengths, scratch);
    }

    public bool MoveNext(out ReadOnlySpan<char> current)
    {
        while (step < Steps.Length)
        {
            var (stage, form) = Steps[step++];
            if (!IsEnabled(stage) || lengths[form] == 0)
                continue;

            var candidate = Compose(stage, form);
            if (candidate.Length == 0 || IsRepeat(candidate))
                continue;

            emittedStarts[emittedCount] = written;
            emittedLengths[emittedCount] = candidate.Length;
            emittedCount++;
            written += candidate.Length;
            current = candidate;
            return true;
        }

        current = default;
        return false;
    }

    readonly bool IsEnabled(byte stage) => stage switch
    {
        PreviousVowelStage => prefixed,
        WildcardStage => wildcard,
        _ => true,
    };

    readonly ReadOnlySpan<char> Compose(byte stage, byte form)
    {
        var body = pool.Slice(starts[form], lengths[form]);
        var prefix = stage switch
        {
            PreviousVowelStage => previousVowel,
            WildcardStage => KanaRomanization.AnyVowel,
            _ => default,
        };

        var length = prefix.Length == 0 ? body.Length : prefix.Length + 1 + body.Length;
        if (written + length > emitted.Length)
            return default;

        var target = emitted.Slice(written, length);
        if (prefix.Length == 0)
        {
            body.CopyTo(target);
            return target;
        }

        prefix.CopyTo(target);
        target[prefix.Length] = KanaRomanization.AliasSeparator;
        body.CopyTo(target[(prefix.Length + 1)..]);
        return target;
    }

    readonly bool IsRepeat(ReadOnlySpan<char> candidate)
    {
        for (var i = 0; i < emittedCount; i++)
        {
            if (emittedLengths[i] != candidate.Length)
                continue;
            if (emitted.Slice(emittedStarts[i], emittedLengths[i]).SequenceEqual(candidate))
                return true;
        }
        return false;
    }

    static void BuildForms(ReadOnlySpan<char> lyric, Span<char> pool, Span<int> starts, Span<int> lengths, Span<char> scratch)
    {
        var written = 0;
        Append(lyric, pool, starts, lengths, 0, ref written);
        Append(KanaRomanization.ToHiragana(lyric, scratch), pool, starts, lengths, 1, ref written);
        Append(KanaRomanization.ToKatakana(lyric, scratch), pool, starts, lengths, 2, ref written);
        Append(KanaRomanization.ToMora(lyric, scratch), pool, starts, lengths, 3, ref written);
        Append(KanaRomanization.ToKatakana(pool.Slice(starts[3], lengths[3]), scratch), pool, starts, lengths, 4, ref written);

        if (KanaRomanization.TryGetRomajiForMora(pool.Slice(starts[3], lengths[3]), out var romaji))
            Append(romaji, pool, starts, lengths, 5, ref written);
        else
            Append(default, pool, starts, lengths, 5, ref written);
    }

    static void Append(ReadOnlySpan<char> text, Span<char> pool, Span<int> starts, Span<int> lengths, int index, ref int written)
    {
        starts[index] = written;
        if (text.Length == 0 || written + text.Length > pool.Length)
        {
            lengths[index] = 0;
            return;
        }

        text.CopyTo(pool[written..]);
        lengths[index] = text.Length;
        written += text.Length;
    }
}

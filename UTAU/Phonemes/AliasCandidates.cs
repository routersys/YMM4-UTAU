namespace UTAU.Phonemes;

internal ref struct AliasCandidates
{
    public const int FormCount = 6;
    public const int BoundsLength = FormCount * 2;

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
    readonly ReadOnlySpan<char> previousVowel;
    readonly bool prefixed;
    readonly bool wildcard;
    int step;

    public AliasCandidates(
        ReadOnlySpan<char> lyric,
        ReadOnlySpan<char> previousVowel,
        Span<char> pool,
        Span<int> bounds,
        Span<char> scratch)
    {
        this.pool = pool;
        this.previousVowel = previousVowel;
        starts = bounds[..FormCount];
        lengths = bounds.Slice(FormCount, FormCount);
        prefixed = previousVowel.Length > 0;
        wildcard = prefixed && !previousVowel.SequenceEqual(KanaRomanization.StartVowel);
        BuildForms(lyric, pool, starts, lengths, scratch);
    }

    public bool MoveNext(Span<char> candidate, out ReadOnlySpan<char> current)
    {
        while (step < Steps.Length)
        {
            var (stage, form) = Steps[step++];
            if (!IsEnabled(stage) || lengths[form] == 0)
                continue;

            current = Compose(stage, form, candidate);
            if (current.Length > 0)
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

    readonly ReadOnlySpan<char> Compose(byte stage, byte form, Span<char> candidate)
    {
        var body = pool.Slice(starts[form], lengths[form]);
        var prefix = stage switch
        {
            PreviousVowelStage => previousVowel,
            WildcardStage => KanaRomanization.AnyVowel,
            _ => default,
        };

        if (prefix.Length == 0)
            return body;

        var length = prefix.Length + 1 + body.Length;
        if (length > candidate.Length)
            return default;

        prefix.CopyTo(candidate);
        candidate[prefix.Length] = KanaRomanization.AliasSeparator;
        body.CopyTo(candidate[(prefix.Length + 1)..]);
        return candidate[..length];
    }

    static void BuildForms(ReadOnlySpan<char> lyric, Span<char> pool, Span<int> starts, Span<int> lengths, Span<char> scratch)
    {
        var written = 0;
        Append(lyric, pool, starts, lengths, 0, ref written);
        Append(KanaRomanization.ToHiragana(lyric, scratch), pool, starts, lengths, 1, ref written);
        Append(KanaRomanization.ToKatakana(lyric, scratch), pool, starts, lengths, 2, ref written);
        Append(KanaRomanization.ToMora(lyric, scratch), pool, starts, lengths, 3, ref written);
        Append(KanaRomanization.ToKatakana(pool.Slice(starts[3], lengths[3]), scratch), pool, starts, lengths, 4, ref written);

        if (KanaRomanization.TryGetRomaji(lyric, scratch, out var romaji))
            Append(romaji, pool, starts, lengths, 5, ref written);
        else
            Append(default, pool, starts, lengths, 5, ref written);

        Deduplicate(pool, starts, lengths);
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

    static void Deduplicate(Span<char> pool, Span<int> starts, Span<int> lengths)
    {
        for (var i = 1; i < FormCount; i++)
        {
            if (lengths[i] == 0)
                continue;

            var current = pool.Slice(starts[i], lengths[i]);
            for (var j = 0; j < i; j++)
            {
                if (lengths[j] != lengths[i])
                    continue;
                if (!pool.Slice(starts[j], lengths[j]).SequenceEqual(current))
                    continue;
                lengths[i] = 0;
                break;
            }
        }
    }
}

using UTAU.Models;
using UTAU.Notes;

namespace UTAU.Phonemes;

internal sealed record PhonemeUnit(
    UTAUNote Note,
    OtoEntry? Entry,
    string Alias,
    double NoteStartMilliseconds,
    double NoteLengthMilliseconds,
    double StartMilliseconds,
    double LengthMilliseconds,
    int Tone)
{
    public bool IsSilent => Entry is null;

    public bool IsUnresolved => Entry is null && !Note.IsRest;

    public double EndMilliseconds => StartMilliseconds + LengthMilliseconds;

    public double Preutterance => Note.PreutteranceOverride > UTAUNote.FollowOtoValue
        ? Note.PreutteranceOverride
        : Entry?.Preutterance ?? 0.0;

    public double Overlap => Note.OverlapOverride > UTAUNote.FollowOtoValue
        ? Note.OverlapOverride
        : Entry?.Overlap ?? 0.0;
}

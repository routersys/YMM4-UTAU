namespace UTAU.Notes;

internal static class UstKeys
{
    public const string VersionHeader = "[#VERSION]";
    public const string SettingHeader = "[#SETTING]";
    public const string TrackEndHeader = "[#TRACKEND]";
    public const string PreviousHeader = "[#PREV]";
    public const string NextHeader = "[#NEXT]";
    public const string InsertHeader = "[#INSERT]";
    public const string DeleteHeader = "[#DELETE]";

    public const string Charset = "Charset";
    public const string UstVersion = "UstVersion";
    public const string Tempo = "Tempo";
    public const string ProjectName = "ProjectName";

    public const string Length = "Length";
    public const string Duration = "Duration";
    public const string Delta = "Delta";
    public const string Lyric = "Lyric";
    public const string NoteNum = "NoteNum";
    public const string Velocity = "Velocity";
    public const string Intensity = "Intensity";
    public const string Modulation = "Modulation";
    public const string Moduration = "Moduration";
    public const string PreUtterance = "PreUtterance";
    public const string VoiceOverlap = "VoiceOverlap";
    public const string StartPoint = "StartPoint";
    public const string Envelope = "Envelope";
    public const string Flags = "Flags";
    public const string Label = "Label";

    public const string PitchBendStart = "PBS";
    public const string PitchBendWidth = "PBW";
    public const string PitchBendY = "PBY";
    public const string PitchBendMode = "PBM";
    public const string Vibrato = "VBR";

    public const string PitchBendType = "PBType";
    public const string LegacyPitchStart = "PBStart";
    public const string LegacyPitchBend = "PitchBend";
    public const string LegacyPitches = "Pitches";
    public const string LegacyPitchesTypo = "Piches";

    public const string RestLyric = "R";
    public const string EnvelopeMarker = "%";
    public const char PitchBendStartSeparator = ';';
    public const char FieldSeparator = ',';

    public const string ShapeLinear = "s";
    public const string ShapeRCurve = "r";
    public const string ShapeJCurve = "j";

    public const char IgnorePrefixMapMarker = '?';
    public const char SuppressAutoVcvMarker = '!';
}

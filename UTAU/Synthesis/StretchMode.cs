using System.ComponentModel.DataAnnotations;

namespace UTAU.Synthesis;

public enum StretchMode
{
    [Display(Name = nameof(Texts.StretchModeLoop), Description = nameof(Texts.StretchModeLoopDescription), ResourceType = typeof(Texts))]
    Loop,

    [Display(Name = nameof(Texts.StretchModeStretch), Description = nameof(Texts.StretchModeStretchDescription), ResourceType = typeof(Texts))]
    Stretch,
}

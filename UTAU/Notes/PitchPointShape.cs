using System.ComponentModel.DataAnnotations;

namespace UTAU.Notes;

internal enum PitchPointShape
{
    [Display(Name = nameof(Texts.PitchShapeSCurve), ResourceType = typeof(Texts))]
    SCurve,

    [Display(Name = nameof(Texts.PitchShapeLinear), ResourceType = typeof(Texts))]
    Linear,

    [Display(Name = nameof(Texts.PitchShapeRCurve), ResourceType = typeof(Texts))]
    RCurve,

    [Display(Name = nameof(Texts.PitchShapeJCurve), ResourceType = typeof(Texts))]
    JCurve,
}

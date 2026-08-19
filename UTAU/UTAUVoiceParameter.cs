using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using UTAU.Views;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace UTAU;

internal sealed class UTAUVoiceParameter : VoiceParameterBase
{
    string color = string.Empty;
    int baseTone = 60;
    double speed = 1.0;
    double tempo = 120.0;
    double volume = 100.0;
    double modulation;
    double formant;
    double breathiness;
    double brightness;

    [Display(Name = nameof(Texts.ParameterColor), Description = nameof(Texts.ParameterColorDescription), ResourceType = typeof(Texts))]
    [VoiceColorComboBox]
    public string Color
    {
        get => color;
        set => Set(ref color, value ?? string.Empty);
    }

    [Display(Name = nameof(Texts.ParameterBaseTone), Description = nameof(Texts.ParameterBaseToneDescription), ResourceType = typeof(Texts))]
    [ToneComboBox]
    [DefaultValue(60)]
    [HideForUstSource]
    public int BaseTone
    {
        get => baseTone;
        set => Set(ref baseTone, Math.Clamp(value, ToneComboBoxAttribute.MinimumTone, ToneComboBoxAttribute.MaximumTone));
    }

    [Display(Name = nameof(Texts.ParameterSpeed), Description = nameof(Texts.ParameterSpeedDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F2", "", 0.2, 3.0, Delay = -1)]
    [Range(0.2, 3.0)]
    [DefaultValue(1.0)]
    public double Speed
    {
        get => speed;
        set => Set(ref speed, value);
    }

    [Display(Name = nameof(Texts.ParameterTempo), Description = nameof(Texts.ParameterTempoDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "BPM", 20.0, 400.0, Delay = -1)]
    [Range(20.0, 400.0)]
    [DefaultValue(120.0)]
    [HideForUstSource]
    public double Tempo
    {
        get => tempo;
        set => Set(ref tempo, value);
    }

    [Display(Name = nameof(Texts.ParameterVolume), Description = nameof(Texts.ParameterVolumeDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", 0.0, 200.0, Delay = -1)]
    [Range(0.0, 200.0)]
    [DefaultValue(100.0)]
    public double Volume
    {
        get => volume;
        set => Set(ref volume, value);
    }

    [Display(Name = nameof(Texts.ParameterModulation), Description = nameof(Texts.ParameterModulationDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", -200.0, 200.0, Delay = -1)]
    [Range(-200.0, 200.0)]
    [DefaultValue(0.0)]
    [HideForUstSource]
    public double Modulation
    {
        get => modulation;
        set => Set(ref modulation, value);
    }

    [Display(Name = nameof(Texts.ParameterFormant), Description = nameof(Texts.ParameterFormantDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F1", "", -12.0, 12.0, Delay = -1)]
    [Range(-12.0, 12.0)]
    [DefaultValue(0.0)]
    public double Formant
    {
        get => formant;
        set => Set(ref formant, value);
    }

    [Display(Name = nameof(Texts.ParameterBreathiness), Description = nameof(Texts.ParameterBreathinessDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "", -100.0, 100.0, Delay = -1)]
    [Range(-100.0, 100.0)]
    [DefaultValue(0.0)]
    public double Breathiness
    {
        get => breathiness;
        set => Set(ref breathiness, value);
    }

    [Display(Name = nameof(Texts.ParameterBrightness), Description = nameof(Texts.ParameterBrightnessDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F1", "dB", -12.0, 12.0, Delay = -1)]
    [Range(-12.0, 12.0)]
    [DefaultValue(0.0)]
    public double Brightness
    {
        get => brightness;
        set => Set(ref brightness, value);
    }
}

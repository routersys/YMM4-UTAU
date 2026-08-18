using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using UTAU;
using UTAU.Notes;
using YukkuriMovieMaker.Controls;

namespace UTAU.Tests;

public sealed class EditorMetadataTests
{
    static readonly Type[] EditedTypes =
    [
        typeof(UTAUNote),
        typeof(PitchPoint),
        typeof(VibratoSettings),
        typeof(UTAUVoiceParameter),
        typeof(UTAUVoicePronounce),
    ];

    static IEnumerable<PropertyInfo> EditableProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute<DisplayAttribute>() is not null);

    public static TheoryData<Type> Types
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in EditedTypes)
                data.Add(type);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void EverySliderDeclaresItsOwnRangeAndDefault(Type type)
    {
        Assert.NotEmpty(EditableProperties(type));
        foreach (var property in EditableProperties(type))
        {
            if (property.GetCustomAttribute<TextBoxSliderAttribute>() is not { } slider)
                continue;

            var range = property.GetCustomAttribute<RangeAttribute>();
            var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();

            Assert.True(range is not null, $"{type.Name}.{property.Name} has no Range");
            Assert.True(defaultValue is not null, $"{type.Name}.{property.Name} has no DefaultValue");
            Assert.True(slider.DefaultMin < slider.DefaultMax, $"{type.Name}.{property.Name} has an empty slider span");

            var minimum = Convert.ToDouble(range!.Minimum);
            var maximum = Convert.ToDouble(range.Maximum);
            var value = Convert.ToDouble(defaultValue!.Value);

            Assert.InRange(value, minimum, maximum);
            Assert.InRange(slider.DefaultMin, minimum, maximum);
            Assert.InRange(slider.DefaultMax, minimum, maximum);
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void EveryDisplayNameResolvesToText(Type type)
    {
        Assert.NotEmpty(EditableProperties(type));
        foreach (var property in EditableProperties(type))
        {
            var display = property.GetCustomAttribute<DisplayAttribute>()!;
            if (display.ResourceType is null)
                continue;

            Assert.False(string.IsNullOrEmpty(display.GetName()), $"{type.Name}.{property.Name} name");
            if (display.Description is not null)
                Assert.False(string.IsNullOrEmpty(display.GetDescription()), $"{type.Name}.{property.Name} description");
            if (display.GroupName is not null)
                Assert.False(string.IsNullOrEmpty(display.GetGroupName()), $"{type.Name}.{property.Name} group");
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void EveryEditablePropertyIsWritableOrExpanded(Type type)
    {
        Assert.NotEmpty(EditableProperties(type));
        foreach (var property in EditableProperties(type))
        {
            var display = property.GetCustomAttribute<DisplayAttribute>()!;
            var isExpanded = display.GetAutoGenerateField() == true;
            var hasEditor = property.GetCustomAttributes().Any(x => x.GetType().Name.EndsWith("EditorAttribute", StringComparison.Ordinal));

            Assert.True(
                property.CanWrite || isExpanded || hasEditor,
                $"{type.Name}.{property.Name} is neither writable nor expanded");
        }
    }

    [Fact]
    public void DefaultsOfANewNoteMatchTheDeclaredDefaults()
    {
        var note = new UTAUNote();
        foreach (var property in EditableProperties(typeof(UTAUNote)))
        {
            if (property.GetCustomAttribute<DefaultValueAttribute>() is not { } declared)
                continue;

            var actual = Convert.ToDouble(property.GetValue(note));
            Assert.Equal(Convert.ToDouble(declared.Value), actual, 9);
        }
    }

    [Fact]
    public void DefaultsOfANewVibratoMatchTheDeclaredDefaults()
    {
        var vibrato = new VibratoSettings();
        foreach (var property in EditableProperties(typeof(VibratoSettings)))
        {
            if (property.GetCustomAttribute<DefaultValueAttribute>() is not { } declared)
                continue;

            var actual = Convert.ToDouble(property.GetValue(vibrato));
            Assert.Equal(Convert.ToDouble(declared.Value), actual, 9);
        }
    }

    [Fact]
    public void DefaultsOfANewParameterMatchTheDeclaredDefaults()
    {
        var parameter = new UTAUVoiceParameter();
        foreach (var property in EditableProperties(typeof(UTAUVoiceParameter)))
        {
            if (property.GetCustomAttribute<DefaultValueAttribute>() is not { } declared)
                continue;

            var actual = Convert.ToDouble(property.GetValue(parameter));
            Assert.Equal(Convert.ToDouble(declared.Value), actual, 9);
        }
    }
}

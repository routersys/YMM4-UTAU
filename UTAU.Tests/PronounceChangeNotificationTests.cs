using System.IO;
using UTAU;
using UTAU.Notes;
using UTAU.ViewModels;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Tests;

public sealed class PronounceChangeNotificationTests
{
    static (UTAUVoicePronounce Pronounce, Func<int> Count) CreateObserved()
    {
        var pronounce = new UTAUVoicePronounce();
        var raised = 0;
        pronounce.UndoRedoCommandCreated += (_, _) => raised++;
        return (pronounce, () => raised);
    }

    [Fact]
    public void AddingANoteNotifiesTheHost()
    {
        var (pronounce, count) = CreateObserved();
        pronounce.Notes.Add(new UTAUNote { Lyric = "あ" });
        Assert.True(count() > 0);
    }

    [Fact]
    public void RemovingANoteNotifiesTheHost()
    {
        var (pronounce, count) = CreateObserved();
        var note = new UTAUNote { Lyric = "あ" };
        pronounce.Notes.Add(note);
        var afterAdd = count();
        pronounce.Notes.Remove(note);
        Assert.True(count() > afterAdd);
    }

    [Theory]
    [InlineData(nameof(UTAUNote.Tone))]
    [InlineData(nameof(UTAUNote.LengthTicks))]
    [InlineData(nameof(UTAUNote.TempoOverride))]
    [InlineData(nameof(UTAUNote.Lyric))]
    [InlineData(nameof(UTAUNote.Velocity))]
    [InlineData(nameof(UTAUNote.Intensity))]
    [InlineData(nameof(UTAUNote.Modulation))]
    [InlineData(nameof(UTAUNote.PreutteranceOverride))]
    [InlineData(nameof(UTAUNote.OverlapOverride))]
    [InlineData(nameof(UTAUNote.StartPointMilliseconds))]
    [InlineData(nameof(UTAUNote.FadeInMilliseconds))]
    [InlineData(nameof(UTAUNote.FadeOutMilliseconds))]
    public void EveryNotePropertyNotifiesTheHost(string propertyName)
    {
        var (pronounce, count) = CreateObserved();
        var note = new UTAUNote { Lyric = "あ" };
        pronounce.Notes.Add(note);
        var afterAdd = count();

        switch (propertyName)
        {
            case nameof(UTAUNote.Tone): note.Tone = 64; break;
            case nameof(UTAUNote.LengthTicks): note.LengthTicks = 960; break;
            case nameof(UTAUNote.TempoOverride): note.TempoOverride = 150.0; break;
            case nameof(UTAUNote.Lyric): note.Lyric = "か"; break;
            case nameof(UTAUNote.Velocity): note.Velocity = 50.0; break;
            case nameof(UTAUNote.Intensity): note.Intensity = 50.0; break;
            case nameof(UTAUNote.Modulation): note.Modulation = 50.0; break;
            case nameof(UTAUNote.PreutteranceOverride): note.PreutteranceOverride = 40.0; break;
            case nameof(UTAUNote.OverlapOverride): note.OverlapOverride = 20.0; break;
            case nameof(UTAUNote.StartPointMilliseconds): note.StartPointMilliseconds = 10.0; break;
            case nameof(UTAUNote.FadeInMilliseconds): note.FadeInMilliseconds = 20.0; break;
            case nameof(UTAUNote.FadeOutMilliseconds): note.FadeOutMilliseconds = 20.0; break;
        }

        Assert.True(count() > afterAdd, propertyName);
    }

    [Fact]
    public void VibratoChangesNotifyTheHost()
    {
        var (pronounce, count) = CreateObserved();
        var note = new UTAUNote { Lyric = "あ" };
        pronounce.Notes.Add(note);
        var afterAdd = count();

        note.Vibrato.LengthPercent = 60.0;
        Assert.True(count() > afterAdd);
    }

    [Fact]
    public void PitchPointChangesNotifyTheHost()
    {
        var (pronounce, count) = CreateObserved();
        var note = new UTAUNote { Lyric = "あ" };
        pronounce.Notes.Add(note);

        var point = new PitchPoint(50, 0.0);
        note.PitchPoints.Add(point);
        var afterAdd = count();

        point.Cents = -120.0;
        Assert.True(count() > afterAdd);
    }

    [Fact]
    public void NotesAddedBeforeTheFirstObserverAreStillTracked()
    {
        var pronounce = new UTAUVoicePronounce();
        var note = new UTAUNote { Lyric = "あ" };
        pronounce.Notes.Add(note);

        var raised = 0;
        pronounce.UndoRedoCommandCreated += (_, _) => raised++;
        note.Tone = 70;

        Assert.True(raised > 0);
    }

    [Fact]
    public void SourceTextChangesNotifyTheHost()
    {
        var (pronounce, count) = CreateObserved();
        pronounce.SourceText = "あ";
        Assert.True(count() > 0);
    }

    [Fact]
    public void RenderMessageChangesReachTheEditor()
    {
        var pronounce = new UTAUVoicePronounce();
        pronounce.Notes.Add(new UTAUNote { Lyric = "あ" });
        using var viewModel = new NoteEditorViewModel(pronounce);
        var raised = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteEditorViewModel.RenderMessage))
                raised++;
        };

        pronounce.RenderMessage = "見つからない";

        Assert.Equal(1, raised);
        Assert.Equal("見つからない", viewModel.RenderMessage);
    }

    [Fact]
    public void TheEditorStopsListeningAfterItIsDisposed()
    {
        var pronounce = new UTAUVoicePronounce();
        pronounce.Notes.Add(new UTAUNote { Lyric = "あ" });
        var viewModel = new NoteEditorViewModel(pronounce);
        var raised = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteEditorViewModel.RenderMessage))
                raised++;
        };
        viewModel.Dispose();

        pronounce.RenderMessage = "見つからない";

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ClonedNotesCarryEveryEditableValue()
    {
        var note = new UTAUNote
        {
            Lyric = "か",
            Tone = 64,
            LengthTicks = 960,
            Velocity = 80.0,
            Intensity = 120.0,
            Modulation = 30.0,
            StartPointMilliseconds = 12.0,
            PreutteranceOverride = 40.0,
            OverlapOverride = 20.0,
            FadeInMilliseconds = 8.0,
            FadeOutMilliseconds = 48.0,
        };
        note.Vibrato.LengthPercent = 60.0;
        note.Vibrato.DepthCents = 80.0;
        note.PitchPoints.Add(new PitchPoint(30, -50.0, PitchPointShape.Linear));

        var clone = note.Clone();

        Assert.Equal(note.Lyric, clone.Lyric);
        Assert.Equal(note.Tone, clone.Tone);
        Assert.Equal(note.LengthTicks, clone.LengthTicks);
        Assert.Equal(note.Velocity, clone.Velocity);
        Assert.Equal(note.Intensity, clone.Intensity);
        Assert.Equal(note.Modulation, clone.Modulation);
        Assert.Equal(note.StartPointMilliseconds, clone.StartPointMilliseconds);
        Assert.Equal(note.PreutteranceOverride, clone.PreutteranceOverride);
        Assert.Equal(note.OverlapOverride, clone.OverlapOverride);
        Assert.Equal(note.FadeInMilliseconds, clone.FadeInMilliseconds);
        Assert.Equal(note.FadeOutMilliseconds, clone.FadeOutMilliseconds);
        Assert.Equal(note.Vibrato.LengthPercent, clone.Vibrato.LengthPercent);
        Assert.Equal(note.Vibrato.DepthCents, clone.Vibrato.DepthCents);
        Assert.Equal(note.PitchPoints[0].Cents, Assert.Single(clone.PitchPoints).Cents);
        Assert.NotSame(note.Vibrato, clone.Vibrato);
    }

    [Fact]
    public void EditingTheCloneDoesNotTouchTheOriginal()
    {
        var note = new UTAUNote { Lyric = "あ" };
        note.PitchPoints.Add(new PitchPoint(10, 0.0));

        var clone = note.Clone();
        clone.Vibrato.DepthCents = 200.0;
        clone.PitchPoints[0].Cents = 999.0;
        clone.Tone = 30;

        Assert.NotEqual(200.0, note.Vibrato.DepthCents);
        Assert.NotEqual(999.0, note.PitchPoints[0].Cents);
        Assert.NotEqual(30, note.Tone);
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UTAU.Models;
using UTAU.Notes;
using YukkuriMovieMaker.Commons;

namespace UTAU.ViewModels;

internal sealed class NoteEditorViewModel : Bindable, IDisposable
{
    public const double MinimumPixelsPerMillisecond = 0.05;
    public const double MaximumPixelsPerMillisecond = 2.0;
    public const double ZoomStep = 1.5;
    public const double DefaultSemitoneHeight = 14.0;
    public const int MinimumVisibleSemitones = 25;
    public const double PitchCurveIntervalMilliseconds = 10.0;
    public const double TimeGridIntervalMilliseconds = 500.0;

    readonly ObservableCollection<UTAUNote> source;
    double pixelsPerMillisecond = 0.25;
    double semitoneHeight = DefaultSemitoneHeight;
    int minimumTone = 48;
    int maximumTone = 72;
    NoteViewModel? selectedNote;
    PointCollection pitchCurve = [];

    public NoteEditorViewModel(ObservableCollection<UTAUNote> notes)
    {
        source = notes;
        Notes = [.. notes.Select(x => new NoteViewModel(x, this))];
        ZoomInCommand = new ActionCommand(_ => PixelsPerMillisecond < MaximumPixelsPerMillisecond, _ => Zoom(ZoomStep));
        ZoomOutCommand = new ActionCommand(_ => PixelsPerMillisecond > MinimumPixelsPerMillisecond, _ => Zoom(1.0 / ZoomStep));
        InsertRestCommand = new ActionCommand(_ => SelectedNote is not null, _ => InsertRest());
        RemoveNoteCommand = new ActionCommand(_ => SelectedNote is not null && Notes.Count > 1, _ => RemoveSelected());
        AddPitchPointCommand = new ActionCommand(_ => SelectedNote is not null, _ => AddPitchPoint());
        RemovePitchPointCommand = new ActionCommand(_ => SelectedPitchPoint is not null, _ => RemovePitchPoint());
        ResetPitchCommand = new ActionCommand(_ => SelectedNote is not null, _ => ResetPitch());
        InvalidateLayout();
        SelectedNote = Notes.FirstOrDefault();
    }

    public ObservableCollection<NoteViewModel> Notes { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand InsertRestCommand { get; }

    public ICommand RemoveNoteCommand { get; }

    public ICommand AddPitchPointCommand { get; }

    public ICommand RemovePitchPointCommand { get; }

    public ICommand ResetPitchCommand { get; }

    public PitchPoint? SelectedPitchPoint { get; set => Set(ref field, value); }

    public double PixelsPerMillisecond
    {
        get => pixelsPerMillisecond;
        private set => Set(ref pixelsPerMillisecond, value);
    }

    public double SemitoneHeight
    {
        get => semitoneHeight;
        private set => Set(ref semitoneHeight, value);
    }

    public int MinimumTone
    {
        get => minimumTone;
        private set => Set(ref minimumTone, value);
    }

    public int MaximumTone
    {
        get => maximumTone;
        private set => Set(ref maximumTone, value);
    }

    public NoteViewModel? SelectedNote
    {
        get => selectedNote;
        set
        {
            if (selectedNote is not null)
                selectedNote.IsSelected = false;
            Set(ref selectedNote, value);
            if (selectedNote is not null)
                selectedNote.IsSelected = true;
            SelectedPitchPoint = null;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(PitchPoints));
        }
    }

    public bool HasSelection => SelectedNote is not null;

    public ObservableCollection<PitchPoint>? PitchPoints => SelectedNote?.Note.PitchPoints;

    public PointCollection PitchCurve
    {
        get => pitchCurve;
        private set => Set(ref pitchCurve, value);
    }

    public double CanvasWidth => Math.Max(TotalMilliseconds * PixelsPerMillisecond, 1.0);

    public double CanvasHeight => (MaximumTone - MinimumTone + 1) * SemitoneHeight;

    public double TotalMilliseconds => Notes.Sum(x => x.Note.LengthMilliseconds);

    public IReadOnlyList<KeyRowViewModel> Keyboard { get; private set; } = [];

    public IReadOnlyList<GridLineViewModel> TimeGridLines { get; private set; } = [];

    public void InvalidateLayout()
    {
        UpdateToneRange();
        var position = 0.0;
        foreach (var note in Notes)
        {
            note.StartMilliseconds = position;
            position += note.Note.LengthMilliseconds;
            note.RaiseLayoutChanged();
        }

        Keyboard = BuildKeyboard();
        TimeGridLines = BuildTimeGridLines();
        OnPropertyChanged(nameof(Keyboard));
        OnPropertyChanged(nameof(TimeGridLines));
        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        OnPropertyChanged(nameof(TotalMilliseconds));
        UpdatePitchCurve();
    }

    public void OnNoteChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(UTAUNote.Tone):
            case nameof(UTAUNote.LengthMilliseconds):
                InvalidateLayout();
                break;
            default:
                UpdatePitchCurve();
                break;
        }
    }

    public void UpdatePitchCurve()
    {
        var points = new PointCollection();
        var position = 0.0;

        foreach (var note in Notes)
        {
            var length = note.Note.LengthMilliseconds;
            if (note.IsRest)
            {
                position += length;
                continue;
            }

            for (var elapsed = 0.0; elapsed <= length; elapsed += PitchCurveIntervalMilliseconds)
            {
                var cents = note.Note.EvaluatePitchOffsetCents(elapsed);
                points.Add(ToCanvasPoint(position + elapsed, note.Note.Tone + cents / 100.0));
            }

            position += length;
        }

        points.Freeze();
        PitchCurve = points;
    }

    public Point ToCanvasPoint(double milliseconds, double tone)
        => new(
            milliseconds * PixelsPerMillisecond,
            (MaximumTone - tone + 0.5) * SemitoneHeight);

    public int ToneFromCanvasY(double y)
        => Math.Clamp((int)Math.Round(MaximumTone - y / SemitoneHeight + 0.5), MinimumTone, MaximumTone);

    public double MillisecondsFromCanvasX(double x) => x / PixelsPerMillisecond;

    IReadOnlyList<KeyRowViewModel> BuildKeyboard()
    {
        var width = CanvasWidth;
        return ComboBoxItems
            .CreateTones(MinimumTone, MaximumTone)
            .Reverse()
            .Select(x => new KeyRowViewModel(x.Name, x.IsAccidental, SemitoneHeight, width))
            .ToArray();
    }

    IReadOnlyList<GridLineViewModel> BuildTimeGridLines()
    {
        var lines = new List<GridLineViewModel>();
        var total = TotalMilliseconds;
        var height = CanvasHeight;
        for (var milliseconds = TimeGridIntervalMilliseconds; milliseconds < total; milliseconds += TimeGridIntervalMilliseconds)
            lines.Add(new GridLineViewModel(milliseconds * PixelsPerMillisecond, height));
        return lines;
    }

    public static IReadOnlyList<PitchShapeItem> PitchShapes { get; } =
    [
        new(Texts.PitchShapeSCurve, PitchPointShape.SCurve),
        new(Texts.PitchShapeLinear, PitchPointShape.Linear),
        new(Texts.PitchShapeRCurve, PitchPointShape.RCurve),
        new(Texts.PitchShapeJCurve, PitchPointShape.JCurve),
    ];

    public void Zoom(double factor)
    {
        PixelsPerMillisecond = Math.Clamp(PixelsPerMillisecond * factor, MinimumPixelsPerMillisecond, MaximumPixelsPerMillisecond);
        InvalidateLayout();
    }

    public void Dispose()
    {
        foreach (var note in Notes)
            note.Dispose();
        Notes.Clear();
    }

    void UpdateToneRange()
    {
        var lowest = Notes.Count == 0 ? MusicalTone.MiddleC.NoteNumber : Notes.Min(x => x.Note.Tone);
        var highest = Notes.Count == 0 ? MusicalTone.MiddleC.NoteNumber : Notes.Max(x => x.Note.Tone);
        var center = (lowest + highest) / 2;
        var span = Math.Max(highest - lowest + 12, MinimumVisibleSemitones);
        MinimumTone = Math.Clamp(center - span / 2, 0, 115);
        MaximumTone = Math.Clamp(MinimumTone + span, MinimumTone + MinimumVisibleSemitones, 127);
    }

    void InsertRest()
    {
        if (SelectedNote is not { } selected)
            return;

        var index = Notes.IndexOf(selected);
        var rest = new UTAUNote
        {
            Lyric = UTAUNote.RestLyric,
            Tone = selected.Note.Tone,
            LengthMilliseconds = UTAUNote.DefaultLengthMilliseconds,
        };
        source.Insert(index + 1, rest);
        Notes.Insert(index + 1, new NoteViewModel(rest, this));
        InvalidateLayout();
    }

    void RemoveSelected()
    {
        if (SelectedNote is not { } selected)
            return;

        var index = Notes.IndexOf(selected);
        source.Remove(selected.Note);
        selected.Dispose();
        Notes.RemoveAt(index);
        SelectedNote = Notes.Count == 0 ? null : Notes[Math.Min(index, Notes.Count - 1)];
        InvalidateLayout();
    }

    void AddPitchPoint()
    {
        if (SelectedNote is not { } selected)
            return;

        var points = selected.Note.PitchPoints;
        var milliseconds = points.Count == 0 ? 0.0 : points[^1].Milliseconds + selected.Note.LengthMilliseconds / 4.0;
        var point = new PitchPoint(Math.Min(milliseconds, selected.Note.LengthMilliseconds), points.Count == 0 ? 0.0 : points[^1].Cents);
        points.Add(point);
        SelectedPitchPoint = point;
        UpdatePitchCurve();
    }

    void RemovePitchPoint()
    {
        if (SelectedNote is not { } selected || SelectedPitchPoint is not { } point)
            return;

        selected.Note.PitchPoints.Remove(point);
        SelectedPitchPoint = selected.Note.PitchPoints.LastOrDefault();
        UpdatePitchCurve();
    }

    void ResetPitch()
    {
        if (SelectedNote is not { } selected)
            return;

        selected.Note.PitchPoints.Clear();
        SelectedPitchPoint = null;
        UpdatePitchCurve();
    }
}

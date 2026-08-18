using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UTAU.Models;
using UTAU.Notes;
using YukkuriMovieMaker.Commons;

namespace UTAU.ViewModels;

internal sealed class NoteEditorViewModel : Bindable, IDisposable
{
    public const double MinimumPixelsPerTick = 0.01;
    public const double MaximumPixelsPerTick = 0.6;
    public const double DefaultPixelsPerTick = 0.08;
    public const double MinimumSemitoneHeight = 8.0;
    public const double MaximumSemitoneHeight = 32.0;
    public const double DefaultSemitoneHeight = 16.0;
    public const double ZoomStep = 1.25;
    public const int MinimumVisibleSemitones = 25;
    public const int PitchCurveIntervalTicks = 10;
    public const double FitMarginRatio = 0.98;
    public const double ViewportChangeThreshold = 2.0;

    readonly ObservableCollection<UTAUNote> source;
    double pixelsPerTick = DefaultPixelsPerTick;
    double semitoneHeight = DefaultSemitoneHeight;
    int minimumTone = 48;
    int maximumTone = 72;
    NoteDivision snapDivision = new(16);
    NoteViewModel? selectedNote;
    ObservableCollection<PitchPoint>? observedPitchPoints;
    PitchPoint? selectedPitchPoint;
    PointCollection pitchCurve = [];
    double guideLeft;
    bool isGuideVisible;
    string guideText = string.Empty;
    bool isAutoFitEnabled = true;
    double lastFitWidth;
    double lastFitHeight;

    public NoteEditorViewModel(ObservableCollection<UTAUNote> notes)
    {
        source = notes;
        Notes = [.. notes.Select(x => new NoteViewModel(x, this))];
        ZoomInCommand = new ActionCommand(_ => pixelsPerTick < MaximumPixelsPerTick, _ => ZoomHorizontally(ZoomStep));
        ZoomOutCommand = new ActionCommand(_ => pixelsPerTick > MinimumPixelsPerTick, _ => ZoomHorizontally(1.0 / ZoomStep));
        ZoomVerticalInCommand = new ActionCommand(_ => semitoneHeight < MaximumSemitoneHeight, _ => ZoomVertically(ZoomStep));
        ZoomVerticalOutCommand = new ActionCommand(_ => semitoneHeight > MinimumSemitoneHeight, _ => ZoomVertically(1.0 / ZoomStep));
        InsertRestCommand = new ActionCommand(_ => SelectedNote is not null, _ => InsertRest());
        RemoveNoteCommand = new ActionCommand(_ => SelectedNote is not null && Notes.Count > 1, _ => RemoveSelected());
        AddPitchPointCommand = new ActionCommand(_ => SelectedNote is not null, _ => AddPitchPoint());
        RemovePitchPointCommand = new ActionCommand(_ => SelectedPitchPoint is not null, _ => RemovePitchPoint());
        ResetPitchCommand = new ActionCommand(_ => SelectedNote is not null, _ => ResetPitch());
        SelectToneCommand = new ActionCommand(_ => SelectedNote is not null, ApplyTone);
        FitCommand = new ActionCommand(_ => true, _ => EnableAutoFit());
        InvalidateLayout();
        SelectedNote = Notes.FirstOrDefault();
    }

    public ObservableCollection<NoteViewModel> Notes { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand ZoomVerticalInCommand { get; }

    public ICommand ZoomVerticalOutCommand { get; }

    public ICommand InsertRestCommand { get; }

    public ICommand RemoveNoteCommand { get; }

    public ICommand AddPitchPointCommand { get; }

    public ICommand RemovePitchPointCommand { get; }

    public ICommand ResetPitchCommand { get; }

    public ICommand SelectToneCommand { get; }

    public ICommand FitCommand { get; }

    public IReadOnlyList<NoteDivision> SnapDivisions => NoteDivision.All;

    public NoteDivision SnapDivision
    {
        get => snapDivision;
        set => Set(ref snapDivision, value);
    }

    public PitchPoint? SelectedPitchPoint
    {
        get => selectedPitchPoint;
        set
        {
            if (Set(ref selectedPitchPoint, value))
                OnPropertyChanged(nameof(HasPitchPoint));
        }
    }

    public bool HasPitchPoint => SelectedPitchPoint is not null;

    public double PixelsPerTick
    {
        get => pixelsPerTick;
        private set => Set(ref pixelsPerTick, value);
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
            ObservePitchPoints(selectedNote?.Note.PitchPoints);
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

    public double GuideLeft
    {
        get => guideLeft;
        private set => Set(ref guideLeft, value);
    }

    public bool IsGuideVisible
    {
        get => isGuideVisible;
        private set => Set(ref isGuideVisible, value);
    }

    public string GuideText
    {
        get => guideText;
        private set => Set(ref guideText, value);
    }

    public double CanvasWidth => Math.Max(TotalTicks * PixelsPerTick, 1.0);

    public double CanvasHeight => (MaximumTone - MinimumTone + 1) * SemitoneHeight;

    public int TotalTicks => Notes.Sum(x => x.Note.LengthTicks);

    public IReadOnlyList<KeyRowViewModel> Keyboard { get; private set; } = [];

    public IReadOnlyList<GridLineViewModel> TimeGridLines { get; private set; } = [];

    public static IReadOnlyList<PitchShapeItem> PitchShapes { get; } =
    [
        new(Texts.PitchShapeSCurve, PitchPointShape.SCurve),
        new(Texts.PitchShapeLinear, PitchPointShape.Linear),
        new(Texts.PitchShapeRCurve, PitchPointShape.RCurve),
        new(Texts.PitchShapeJCurve, PitchPointShape.JCurve),
    ];

    public void InvalidateLayout()
    {
        UpdateToneRange();
        var position = 0;
        foreach (var note in Notes)
        {
            note.StartTicks = position;
            position += note.Note.LengthTicks;
            note.RaiseLayoutChanged();
        }

        Keyboard = BuildKeyboard();
        TimeGridLines = BuildTimeGridLines();
        OnPropertyChanged(nameof(Keyboard));
        OnPropertyChanged(nameof(TimeGridLines));
        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        OnPropertyChanged(nameof(TotalTicks));
        UpdatePitchCurve();
    }

    public void OnNoteChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(UTAUNote.Tone):
            case nameof(UTAUNote.LengthTicks):
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
        var position = 0;

        foreach (var note in Notes)
        {
            var length = note.Note.LengthTicks;
            if (note.IsRest)
            {
                position += length;
                continue;
            }

            for (var elapsed = 0; elapsed <= length; elapsed += PitchCurveIntervalTicks)
            {
                var cents = note.Note.EvaluatePitchOffsetCents(elapsed, length);
                points.Add(ToCanvasPoint(position + elapsed, note.Note.Tone + cents / 100.0));
            }

            position += length;
        }

        points.Freeze();
        PitchCurve = points;
    }

    public Point ToCanvasPoint(double ticks, double tone)
        => new(ticks * PixelsPerTick, (MaximumTone - tone + 0.5) * SemitoneHeight);

    public int ToneFromCanvasY(double y)
        => Math.Clamp((int)Math.Round(MaximumTone - y / SemitoneHeight + 0.5), 0, 127);

    public int TicksFromCanvasX(double x) => (int)Math.Round(x / PixelsPerTick);

    public void UpdateGuide(Point position)
    {
        var ticks = Math.Max(TicksFromCanvasX(position.X), 0);
        var snapped = SnapDivision.IsFree ? ticks : SnapDivision.Snap(ticks);
        GuideLeft = snapped * PixelsPerTick;
        GuideText = FormatPosition(snapped, ToneFromCanvasY(position.Y));
        IsGuideVisible = true;
    }

    public void HideGuide()
    {
        IsGuideVisible = false;
        GuideText = string.Empty;
    }

    static string FormatPosition(int ticks, int tone)
    {
        var bar = ticks / TimeBase.TicksPerWholeNote + 1;
        var beat = ticks % TimeBase.TicksPerWholeNote / TimeBase.TicksPerQuarterNote + 1;
        return $"{new MusicalTone(tone).Name}  {bar}:{beat}";
    }

    public int SnapLength(int ticks)
        => Math.Clamp(SnapDivision.Snap(ticks), UTAUNote.MinimumLengthTicks, UTAUNote.MaximumLengthTicks);

    public void ZoomHorizontally(double factor)
    {
        isAutoFitEnabled = false;
        PixelsPerTick = Math.Clamp(PixelsPerTick * factor, MinimumPixelsPerTick, MaximumPixelsPerTick);
        InvalidateLayout();
    }

    public void ZoomVertically(double factor)
    {
        isAutoFitEnabled = false;
        SemitoneHeight = Math.Clamp(SemitoneHeight * factor, MinimumSemitoneHeight, MaximumSemitoneHeight);
        InvalidateLayout();
    }

    public static double CalculatePixelsPerTick(double viewportWidth, int totalTicks)
        => Math.Clamp(
            viewportWidth * FitMarginRatio / Math.Max(totalTicks, 1),
            MinimumPixelsPerTick,
            MaximumPixelsPerTick);

    public static double CalculateSemitoneHeight(double viewportHeight, int visibleSemitones)
        => Math.Clamp(
            viewportHeight * FitMarginRatio / Math.Max(visibleSemitones, 1),
            MinimumSemitoneHeight,
            MaximumSemitoneHeight);

    public bool FitToViewport(double viewportWidth, double viewportHeight)
    {
        if (!isAutoFitEnabled || viewportWidth <= 0.0 || viewportHeight <= 0.0)
            return false;
        if (Math.Abs(viewportWidth - lastFitWidth) < ViewportChangeThreshold
            && Math.Abs(viewportHeight - lastFitHeight) < ViewportChangeThreshold)
            return false;

        lastFitWidth = viewportWidth;
        lastFitHeight = viewportHeight;
        PixelsPerTick = CalculatePixelsPerTick(viewportWidth, TotalTicks);
        SemitoneHeight = CalculateSemitoneHeight(viewportHeight, MaximumTone - MinimumTone + 1);
        InvalidateLayout();
        return true;
    }

    public void EnableAutoFit()
    {
        isAutoFitEnabled = true;
        lastFitWidth = 0.0;
        lastFitHeight = 0.0;
    }

    public void Dispose()
    {
        ObservePitchPoints(null);
        foreach (var note in Notes)
            note.Dispose();
        Notes.Clear();
    }

    void ObservePitchPoints(ObservableCollection<PitchPoint>? points)
    {
        if (ReferenceEquals(observedPitchPoints, points))
            return;

        if (observedPitchPoints is not null)
        {
            observedPitchPoints.CollectionChanged -= OnPitchPointsChanged;
            foreach (var point in observedPitchPoints)
                point.PropertyChanged -= OnPitchPointChanged;
        }

        observedPitchPoints = points;

        if (observedPitchPoints is null)
            return;

        observedPitchPoints.CollectionChanged += OnPitchPointsChanged;
        foreach (var point in observedPitchPoints)
            point.PropertyChanged += OnPitchPointChanged;
    }

    void OnPitchPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var point in e.OldItems?.OfType<PitchPoint>() ?? [])
            point.PropertyChanged -= OnPitchPointChanged;
        foreach (var point in e.NewItems?.OfType<PitchPoint>() ?? [])
            point.PropertyChanged += OnPitchPointChanged;
        UpdatePitchCurve();
    }

    void OnPitchPointChanged(object? sender, PropertyChangedEventArgs e) => UpdatePitchCurve();

    IReadOnlyList<KeyRowViewModel> BuildKeyboard()
    {
        var width = CanvasWidth;
        var height = SemitoneHeight;
        var rows = new List<KeyRowViewModel>(MaximumTone - MinimumTone + 1);
        for (var noteNumber = MaximumTone; noteNumber >= MinimumTone; noteNumber--)
        {
            var name = new MusicalTone(noteNumber).Name;
            rows.Add(new KeyRowViewModel(name, name.Contains('#'), noteNumber, height, width));
        }
        return rows;
    }

    IReadOnlyList<GridLineViewModel> BuildTimeGridLines()
    {
        var lines = new List<GridLineViewModel>();
        var total = TotalTicks;
        var height = CanvasHeight;
        for (var ticks = TimeBase.TicksPerQuarterNote; ticks < total; ticks += TimeBase.TicksPerQuarterNote)
            lines.Add(new GridLineViewModel(ticks * PixelsPerTick, height, ticks % TimeBase.TicksPerWholeNote == 0));
        return lines;
    }

    void UpdateToneRange()
    {
        var lowest = Notes.Count == 0 ? MusicalTone.MiddleC.NoteNumber : Notes.Min(x => x.Note.Tone);
        var highest = Notes.Count == 0 ? MusicalTone.MiddleC.NoteNumber : Notes.Max(x => x.Note.Tone);
        var span = Math.Max(highest - lowest + 12, MinimumVisibleSemitones);
        var center = (lowest + highest) / 2;
        MinimumTone = Math.Clamp(center - span / 2, 0, 127 - span);
        MaximumTone = MinimumTone + span;
    }

    void ApplyTone(object? parameter)
    {
        if (SelectedNote is not { } selected || parameter is not KeyRowViewModel row)
            return;
        selected.Tone = row.NoteNumber;
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
            LengthTicks = SnapDivision.IsFree ? UTAUNote.DefaultLengthTicks : SnapDivision.Ticks,
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
        var milliseconds = points.Count == 0 ? 0.0 : points[^1].Milliseconds + 100.0;
        var point = new PitchPoint(milliseconds, points.Count == 0 ? 0.0 : points[^1].Cents);
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

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
    public const double StripHeight = 72.0;
    public const double PitchHandleSize = 8.0;

    readonly UTAUVoicePronounce pronounce;
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
    PointCollection expressionCurvePoints = [];
    ExpressionItem selectedExpression = ExpressionItem.All[1];
    double[]? workingCurve;
    double guideLeft;
    bool isGuideVisible;
    string guideText = string.Empty;
    bool isAutoFitEnabled = true;
    double lastFitWidth;
    double lastFitHeight;

    public NoteEditorViewModel(UTAUVoicePronounce pronounce)
    {
        this.pronounce = pronounce;
        source = pronounce.Notes;
        Notes = [.. source.Select(x => new NoteViewModel(x, this))];
        ZoomInCommand = new ActionCommand(_ => pixelsPerTick < MaximumPixelsPerTick, _ => ZoomHorizontally(ZoomStep));
        ZoomOutCommand = new ActionCommand(_ => pixelsPerTick > MinimumPixelsPerTick, _ => ZoomHorizontally(1.0 / ZoomStep));
        ZoomVerticalInCommand = new ActionCommand(_ => semitoneHeight < MaximumSemitoneHeight, _ => ZoomVertically(ZoomStep));
        ZoomVerticalOutCommand = new ActionCommand(_ => semitoneHeight > MinimumSemitoneHeight, _ => ZoomVertically(1.0 / ZoomStep));
        InsertRestCommand = new ActionCommand(_ => SelectedNote is not null, _ => InsertRest());
        RemoveNoteCommand = new ActionCommand(_ => SelectedNote is not null && Notes.Count > 1, _ => RemoveSelected());
        AddPitchPointCommand = new ActionCommand(_ => SelectedNote is not null, _ => AddPitchPoint());
        RemovePitchPointCommand = new ActionCommand(_ => SelectedPitchPoint is not null, _ => RemoveSelectedPitchPoint());
        ResetPitchCommand = new ActionCommand(_ => SelectedNote is not null, _ => ResetPitch());
        ResetExpressionCommand = new ActionCommand(_ => true, _ => ResetExpression());
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

    public ICommand ResetExpressionCommand { get; }

    public ICommand SelectToneCommand { get; }

    public ICommand FitCommand { get; }

    public TimeBase Time => pronounce.TimeBase;

    public IReadOnlyList<NoteDivision> SnapDivisions => NoteDivision.All;

    public IReadOnlyList<ExpressionItem> Expressions => ExpressionItem.All;

    public NoteDivision SnapDivision
    {
        get => snapDivision;
        set => Set(ref snapDivision, value);
    }

    public ExpressionItem SelectedExpression
    {
        get => selectedExpression;
        set
        {
            if (!Set(ref selectedExpression, value ?? ExpressionItem.All[0]))
                return;

            workingCurve = null;
            OnPropertyChanged(nameof(IsCurveExpression));
            UpdateExpression();
        }
    }

    public bool IsCurveExpression => SelectedExpression.IsCurve;

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
            UpdatePitchHandles();
        }
    }

    public bool HasSelection => SelectedNote is not null;

    public PointCollection PitchCurve
    {
        get => pitchCurve;
        private set => Set(ref pitchCurve, value);
    }

    public PointCollection ExpressionCurvePoints
    {
        get => expressionCurvePoints;
        private set => Set(ref expressionCurvePoints, value);
    }

    public IReadOnlyList<ExpressionBarViewModel> ExpressionBars { get; private set; } = [];

    public IReadOnlyList<PitchHandleViewModel> PitchHandles { get; private set; } = [];

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

    public double StripCanvasHeight => StripHeight;

    public int TotalTicks => Notes.Sum(x => x.Note.LengthTicks);

    public IReadOnlyList<KeyRowViewModel> Keyboard { get; private set; } = [];

    public IReadOnlyList<GridLineViewModel> TimeGridLines { get; private set; } = [];

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
        UpdatePitchHandles();
        UpdateExpression();
    }

    public void OnNoteChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(UTAUNote.Tone):
            case nameof(UTAUNote.LengthTicks):
                InvalidateLayout();
                break;
            case nameof(UTAUNote.Velocity):
            case nameof(UTAUNote.Intensity):
            case nameof(UTAUNote.Modulation):
                UpdateExpression();
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

            var lengthMilliseconds = Time.ToMilliseconds(length);
            for (var elapsed = 0; elapsed <= length; elapsed += PitchCurveIntervalTicks)
            {
                var cents = note.Note.EvaluatePitchOffsetCents(elapsed / (double)length, lengthMilliseconds);
                points.Add(ToCanvasPoint(position + elapsed, note.Note.Tone + cents / 100.0));
            }

            position += length;
        }

        points.Freeze();
        PitchCurve = points;
    }

    public void UpdatePitchHandles()
    {
        if (SelectedNote is not { } selected || selected.IsRest)
        {
            PitchHandles = [];
            OnPropertyChanged(nameof(PitchHandles));
            return;
        }

        var handles = new List<PitchHandleViewModel>(selected.Note.PitchPoints.Count);
        foreach (var point in selected.Note.PitchPoints)
        {
            var position = ToCanvasPoint(selected.StartTicks + point.Ticks, selected.Note.Tone + point.Cents / 100.0);
            handles.Add(new PitchHandleViewModel(
                point,
                position.X - PitchHandleSize / 2.0,
                position.Y - PitchHandleSize / 2.0,
                PitchHandleSize));
        }

        PitchHandles = handles;
        OnPropertyChanged(nameof(PitchHandles));
    }

    public void UpdateExpression()
    {
        if (SelectedExpression.IsCurve)
        {
            ExpressionBars = [];
            ExpressionCurvePoints = BuildExpressionCurve(workingCurve);
        }
        else
        {
            ExpressionCurvePoints = [];
            ExpressionBars = BuildExpressionBars();
        }

        OnPropertyChanged(nameof(ExpressionBars));
    }

    public Point ToCanvasPoint(double ticks, double tone)
        => new(ticks * PixelsPerTick, (MaximumTone - tone + 0.5) * SemitoneHeight);

    public int ToneFromCanvasY(double y)
        => Math.Clamp((int)Math.Round(MaximumTone - y / SemitoneHeight + 0.5), 0, 127);

    public double CentsFromCanvasY(double y, int tone)
        => Math.Clamp((MaximumTone - y / SemitoneHeight + 0.5 - tone) * 100.0, PitchPoint.MinimumCents, PitchPoint.MaximumCents);

    public int TicksFromCanvasX(double x) => (int)Math.Round(x / PixelsPerTick);

    public int SnapLength(int ticks)
        => Math.Clamp(SnapDivision.Snap(ticks), UTAUNote.MinimumLengthTicks, UTAUNote.MaximumLengthTicks);

    public NoteViewModel? FindNoteAt(int ticks)
        => Notes.FirstOrDefault(x => ticks >= x.StartTicks && ticks < x.EndTicks);

    public void SetExpressionAt(int ticks, double ratio)
    {
        if (SelectedExpression.IsCurve)
        {
            PaintCurve(ticks, SelectedExpression.FromRatio(ratio));
            return;
        }

        if (FindNoteAt(ticks) is not { } note || note.IsRest)
            return;

        var value = SelectedExpression.FromRatio(ratio);
        switch (SelectedExpression.NoteExpression)
        {
            case NoteExpression.Velocity: note.Note.Velocity = value; break;
            case NoteExpression.Intensity: note.Note.Intensity = value; break;
            case NoteExpression.Modulation: note.Note.Modulation = value; break;
        }
    }

    public void CommitExpression()
    {
        if (workingCurve is null)
            return;

        CurrentCurve.Commit(workingCurve);
        workingCurve = null;
        UpdateExpression();
    }

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
        => Math.Clamp(viewportWidth * FitMarginRatio / Math.Max(totalTicks, 1), MinimumPixelsPerTick, MaximumPixelsPerTick);

    public static double CalculateSemitoneHeight(double viewportHeight, int visibleSemitones)
        => Math.Clamp(viewportHeight * FitMarginRatio / Math.Max(visibleSemitones, 1), MinimumSemitoneHeight, MaximumSemitoneHeight);

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

    public void UpdateGuide(Point position)
    {
        var ticks = Math.Max(TicksFromCanvasX(position.X), 0);
        var snapped = SnapDivision.IsFree ? ticks : SnapDivision.Snap(ticks);
        GuideLeft = snapped * PixelsPerTick;
        GuideText = FormatPosition(snapped, ToneFromCanvasY(position.Y));
        IsGuideVisible = true;
    }

    public void ShowPitchGuide(PitchPoint point)
    {
        GuideLeft = (SelectedNote?.StartTicks ?? 0) * PixelsPerTick + point.Ticks * PixelsPerTick;
        GuideText = string.Format(Texts.PitchGuideFormat, point.Ticks, Math.Round(point.Cents));
        IsGuideVisible = true;
    }

    public void HideGuide()
    {
        IsGuideVisible = false;
        GuideText = string.Empty;
    }

    public void AddPitchPointAt(int ticksFromNoteStart, double cents)
    {
        if (SelectedNote is not { } selected || selected.IsRest)
            return;

        var point = new PitchPoint(ticksFromNoteStart, cents);
        var index = 0;
        while (index < selected.Note.PitchPoints.Count && selected.Note.PitchPoints[index].Ticks <= point.Ticks)
            index++;
        selected.Note.PitchPoints.Insert(index, point);
        SelectedPitchPoint = point;
    }

    public void MovePitchPoint(PitchPoint point, int ticksFromNoteStart, double cents)
    {
        point.Ticks = ticksFromNoteStart;
        point.Cents = cents;
    }

    public void RemovePitchPoint(PitchPoint point)
    {
        if (SelectedNote is not { } selected)
            return;

        selected.Note.PitchPoints.Remove(point);
        if (ReferenceEquals(SelectedPitchPoint, point))
            SelectedPitchPoint = selected.Note.PitchPoints.LastOrDefault();
    }

    public void Dispose()
    {
        ObservePitchPoints(null);
        foreach (var note in Notes)
            note.Dispose();
        Notes.Clear();
    }

    ExpressionCurve CurrentCurve
        => SelectedExpression.CurveExpression == CurveExpression.Formant
            ? pronounce.FormantCurve
            : pronounce.BreathinessCurve;

    void PaintCurve(int ticks, double value)
    {
        workingCurve ??= CurrentCurve.CreateWorkingCopy(TotalTicks);
        var index = ExpressionCurve.ToIndex(ticks);
        if (index < 0 || index >= workingCurve.Length)
            return;

        workingCurve[index] = value;
        ExpressionCurvePoints = BuildExpressionCurve(workingCurve);
    }

    PointCollection BuildExpressionCurve(double[]? samples)
    {
        var values = samples ?? CurrentCurve.Values;
        var expression = SelectedExpression;
        var points = new PointCollection();
        var total = Math.Max(TotalTicks, ExpressionCurve.IntervalTicks);

        for (var ticks = 0; ticks <= total; ticks += ExpressionCurve.IntervalTicks)
        {
            var index = ticks / ExpressionCurve.IntervalTicks;
            var value = values.Length == 0 ? 0.0 : values[Math.Clamp(index, 0, values.Length - 1)];
            points.Add(new Point(ticks * PixelsPerTick, (1.0 - expression.ToRatio(value)) * StripHeight));
        }

        points.Freeze();
        return points;
    }

    IReadOnlyList<ExpressionBarViewModel> BuildExpressionBars()
    {
        var expression = SelectedExpression;
        var baseline = (1.0 - expression.Baseline) * StripHeight;
        var bars = new List<ExpressionBarViewModel>(Notes.Count);

        foreach (var note in Notes)
        {
            if (note.IsRest)
                continue;

            var value = expression.NoteExpression switch
            {
                NoteExpression.Velocity => note.Note.Velocity,
                NoteExpression.Modulation => note.Note.Modulation,
                _ => note.Note.Intensity,
            };
            var y = (1.0 - expression.ToRatio(value)) * StripHeight;
            bars.Add(new ExpressionBarViewModel(
                note,
                note.Left,
                note.Width,
                Math.Min(y, baseline),
                Math.Max(Math.Abs(baseline - y), 1.0)));
        }

        return bars;
    }

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
        var ticks = points.Count == 0
            ? 0
            : Math.Min(points[^1].Ticks + selected.Note.LengthTicks / 4, selected.Note.LengthTicks);
        AddPitchPointAt(ticks, points.Count == 0 ? 0.0 : points[^1].Cents);
    }

    void RemoveSelectedPitchPoint()
    {
        if (SelectedPitchPoint is { } point)
            RemovePitchPoint(point);
    }

    void ResetPitch()
    {
        if (SelectedNote is not { } selected)
            return;

        selected.Note.PitchPoints.Clear();
        SelectedPitchPoint = null;
    }

    void ResetExpression()
    {
        if (SelectedExpression.IsCurve)
        {
            workingCurve = null;
            CurrentCurve.Values = [];
            UpdateExpression();
            return;
        }

        foreach (var note in Notes)
        {
            switch (SelectedExpression.NoteExpression)
            {
                case NoteExpression.Velocity: note.Note.Velocity = 100.0; break;
                case NoteExpression.Intensity: note.Note.Intensity = 100.0; break;
                case NoteExpression.Modulation: note.Note.Modulation = 0.0; break;
            }
        }
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
        UpdatePitchHandles();
    }

    void OnPitchPointChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdatePitchCurve();
        UpdatePitchHandles();
    }

    static string FormatPosition(int ticks, int tone)
    {
        var bar = ticks / TimeBase.TicksPerWholeNote + 1;
        var beat = ticks % TimeBase.TicksPerWholeNote / TimeBase.TicksPerQuarterNote + 1;
        return $"{new MusicalTone(tone).Name}  {bar}:{beat}";
    }
}

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
    public const double SelectionBoxThreshold = 3.0;

    readonly UTAUVoicePronounce pronounce;
    readonly ObservableCollection<UTAUNote> source;
    readonly List<NoteViewModel> selectedNotes = [];
    readonly List<NoteViewModel> transformTargets = [];
    double pixelsPerTick = DefaultPixelsPerTick;
    double semitoneHeight = DefaultSemitoneHeight;
    int minimumTone = 48;
    int maximumTone = 72;
    NoteDivision snapDivision = new(16);
    NoteViewModel? selectedNote;
    int[] transformOriginTones = [];
    int[] transformOriginLengths = [];
    Rect selectionBox;
    bool isSelectionBoxVisible;
    bool isBatching;
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
    string ustPath = string.Empty;
    string importMessage = string.Empty;

    public NoteEditorViewModel(UTAUVoicePronounce pronounce)
    {
        this.pronounce = pronounce;
        source = pronounce.Notes;
        Notes = [.. source.Select(x => new NoteViewModel(x, this))];
        ZoomInCommand = new ActionCommand(_ => pixelsPerTick < MaximumPixelsPerTick, _ => ZoomHorizontally(ZoomStep));
        ZoomOutCommand = new ActionCommand(_ => pixelsPerTick > MinimumPixelsPerTick, _ => ZoomHorizontally(1.0 / ZoomStep));
        ZoomVerticalInCommand = new ActionCommand(_ => semitoneHeight < MaximumSemitoneHeight, _ => ZoomVertically(ZoomStep));
        ZoomVerticalOutCommand = new ActionCommand(_ => semitoneHeight > MinimumSemitoneHeight, _ => ZoomVertically(1.0 / ZoomStep));
        InsertRestCommand = new ActionCommand(_ => selectedNotes.Count > 0, _ => InsertRest());
        RemoveNoteCommand = new ActionCommand(_ => selectedNotes.Count > 0 && Notes.Count > selectedNotes.Count, _ => RemoveSelected());
        SelectAllCommand = new ActionCommand(_ => Notes.Count > 0, _ => SelectAll());
        ImportUstCommand = new ActionCommand(_ => ustPath.Length > 0, _ => ImportUst());
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

    public ICommand SelectAllCommand { get; }

    public ICommand ImportUstCommand { get; }

    public string UstPath
    {
        get => ustPath;
        set => Set(ref ustPath, value ?? string.Empty);
    }

    public string ImportMessage
    {
        get => importMessage;
        private set => Set(ref importMessage, value);
    }

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
        set => Select(value);
    }

    public IReadOnlyList<NoteViewModel> SelectedNotes => selectedNotes;

    public IReadOnlyList<UTAUNote> SelectedNoteTargets { get; private set; } = [];

    public int SelectedCount => selectedNotes.Count;

    public bool HasSelection => selectedNote is not null;

    public bool HasMultipleSelection => selectedNotes.Count > 1;

    public string SelectionText
        => selectedNotes.Count > 1 ? string.Format(Texts.SelectionCountFormat, selectedNotes.Count) : string.Empty;

    public double SelectionBoxLeft => selectionBox.X;

    public double SelectionBoxTop => selectionBox.Y;

    public double SelectionBoxWidth => selectionBox.Width;

    public double SelectionBoxHeight => selectionBox.Height;

    public bool IsSelectionBoxVisible
    {
        get => isSelectionBoxVisible;
        private set => Set(ref isSelectionBoxVisible, value);
    }

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
        if (isBatching)
            return;

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

    public void Select(NoteViewModel? note)
    {
        ClearSelection();
        if (note is not null)
            AddToSelection(note);
        SetPrimary(note);
    }

    public void MakePrimary(NoteViewModel note)
    {
        if (selectedNotes.Contains(note))
            SetPrimary(note);
        else
            Select(note);
    }

    public void ToggleSelection(NoteViewModel note)
    {
        if (!selectedNotes.Remove(note))
        {
            AddToSelection(note);
            SetPrimary(note);
            return;
        }

        note.IsSelected = false;
        SetPrimary(ReferenceEquals(selectedNote, note) ? selectedNotes.LastOrDefault() : selectedNote);
    }

    public void SelectRange(NoteViewModel note)
    {
        var target = Notes.IndexOf(note);
        if (target < 0)
            return;

        var anchor = selectedNote is null ? target : Notes.IndexOf(selectedNote);
        if (anchor < 0)
            anchor = target;

        ClearSelection();
        for (var index = Math.Min(anchor, target); index <= Math.Max(anchor, target); index++)
            AddToSelection(Notes[index]);
        SetPrimary(note);
    }

    public void SelectAll()
    {
        var primary = selectedNote;
        ClearSelection();
        foreach (var note in Notes)
            AddToSelection(note);
        SetPrimary(primary ?? Notes.FirstOrDefault());
    }

    public void SelectInBox(Rect box, bool add)
    {
        if (!add)
            ClearSelection();

        foreach (var note in Notes)
        {
            if (box.IntersectsWith(new Rect(note.Left, note.Top, note.Width, note.Height)))
                AddToSelection(note);
        }

        SetPrimary(selectedNote is not null && selectedNotes.Contains(selectedNote)
            ? selectedNote
            : selectedNotes.LastOrDefault());
    }

    public void UpdateSelectionBox(Point origin, Point current)
    {
        SetSelectionBox(new Rect(origin, current));
        IsSelectionBoxVisible = true;
    }

    public void CommitSelectionBox(bool add)
    {
        var box = selectionBox;
        HideSelectionBox();
        if (box.Width < SelectionBoxThreshold && box.Height < SelectionBoxThreshold)
            return;

        SelectInBox(box, add);
    }

    public void HideSelectionBox()
    {
        SetSelectionBox(default);
        IsSelectionBoxVisible = false;
    }

    public void BeginTransform()
    {
        transformTargets.Clear();
        transformTargets.AddRange(selectedNotes);
        transformOriginTones = [.. transformTargets.Select(x => x.Note.Tone)];
        transformOriginLengths = [.. transformTargets.Select(x => x.Note.LengthTicks)];
    }

    public void TransformTones(int deltaSemitones)
    {
        if (transformTargets.Count == 0)
            return;

        var shift = Math.Clamp(deltaSemitones, -transformOriginTones.Min(), 127 - transformOriginTones.Max());
        Batch(() =>
        {
            for (var index = 0; index < transformTargets.Count; index++)
                transformTargets[index].Tone = transformOriginTones[index] + shift;
        });
    }

    public void TransformLengths(int deltaTicks)
    {
        if (transformTargets.Count == 0)
            return;

        Batch(() =>
        {
            for (var index = 0; index < transformTargets.Count; index++)
                transformTargets[index].LengthTicks = SnapLength(transformOriginLengths[index] + deltaTicks);
        });
    }

    public void EndTransform()
    {
        transformTargets.Clear();
        transformOriginTones = [];
        transformOriginLengths = [];
    }

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
        selectedNotes.Clear();
        transformTargets.Clear();
        foreach (var note in Notes)
            note.Dispose();
        Notes.Clear();
    }

    void ClearSelection()
    {
        foreach (var note in selectedNotes)
            note.IsSelected = false;
        selectedNotes.Clear();
    }

    void AddToSelection(NoteViewModel note)
    {
        if (selectedNotes.Contains(note))
            return;

        note.IsSelected = true;
        selectedNotes.Add(note);
    }

    void SetPrimary(NoteViewModel? note)
    {
        if (selectedNote is not null)
            selectedNote.IsPrimary = false;
        Set(ref selectedNote, note);
        if (selectedNote is not null)
            selectedNote.IsPrimary = true;

        SelectedNoteTargets = [.. selectedNotes.Select(x => x.Note)];
        ObservePitchPoints(selectedNote?.Note.PitchPoints);
        SelectedPitchPoint = null;
        OnPropertyChanged(nameof(SelectedNotes));
        OnPropertyChanged(nameof(SelectedNoteTargets));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(SelectionText));
        UpdatePitchHandles();
    }

    void SetSelectionBox(Rect box)
    {
        selectionBox = box;
        OnPropertyChanged(nameof(SelectionBoxLeft));
        OnPropertyChanged(nameof(SelectionBoxTop));
        OnPropertyChanged(nameof(SelectionBoxWidth));
        OnPropertyChanged(nameof(SelectionBoxHeight));
    }

    void Batch(Action action)
    {
        if (isBatching)
        {
            action();
            return;
        }

        isBatching = true;
        try
        {
            action();
        }
        finally
        {
            isBatching = false;
        }

        InvalidateLayout();
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
        var span = Math.Clamp(highest - lowest + 12, MinimumVisibleSemitones, 127);
        var center = (lowest + highest) / 2;
        MinimumTone = Math.Clamp(center - span / 2, 0, 127 - span);
        MaximumTone = MinimumTone + span;
    }

    void ApplyTone(object? parameter)
    {
        if (parameter is not KeyRowViewModel row || selectedNote is null || selectedNotes.Count == 0)
            return;

        var shift = Math.Clamp(
            row.NoteNumber - selectedNote.Note.Tone,
            -selectedNotes.Min(x => x.Note.Tone),
            127 - selectedNotes.Max(x => x.Note.Tone));
        if (shift == 0)
            return;

        Batch(() =>
        {
            foreach (var note in selectedNotes)
                note.Tone += shift;
        });
    }

    void InsertRest()
    {
        if (selectedNotes.Count == 0)
            return;

        var index = selectedNotes.Max(Notes.IndexOf);
        var rest = new UTAUNote
        {
            Lyric = UTAUNote.RestLyric,
            Tone = selectedNote?.Note.Tone ?? MusicalTone.MiddleC.NoteNumber,
            LengthTicks = SnapDivision.IsFree ? UTAUNote.DefaultLengthTicks : SnapDivision.Ticks,
        };
        source.Insert(index + 1, rest);
        Notes.Insert(index + 1, new NoteViewModel(rest, this));
        InvalidateLayout();
    }

    void RemoveSelected()
    {
        if (selectedNotes.Count == 0 || Notes.Count <= selectedNotes.Count)
            return;

        var removing = selectedNotes.OrderBy(Notes.IndexOf).ToArray();
        var index = Notes.IndexOf(removing[0]);
        Select(null);

        foreach (var note in removing)
        {
            source.Remove(note.Note);
            Notes.Remove(note);
            note.Dispose();
        }

        Select(Notes[Math.Min(index, Notes.Count - 1)]);
        InvalidateLayout();
    }

    void ImportUst()
    {
        if (UstParser.ParseFile(UstPath) is not { } document)
        {
            ImportMessage = Texts.UstImportFailed;
            return;
        }

        var result = UstImporter.Import(document);
        if (result.Notes.Count == 0)
        {
            ImportMessage = Texts.UstImportEmpty;
            return;
        }

        ReplaceNotes(result);
        ImportMessage = BuildImportMessage(result);
    }

    void ReplaceNotes(UstImportResult result)
    {
        Select(null);
        for (var index = Notes.Count - 1; index >= 0; index--)
        {
            var note = Notes[index];
            source.RemoveAt(index);
            Notes.RemoveAt(index);
            note.Dispose();
        }

        foreach (var note in result.Notes)
        {
            source.Add(note);
            Notes.Add(new NoteViewModel(note, this));
        }

        pronounce.Tempo = result.Tempo;
        EnableAutoFit();
        InvalidateLayout();
        Select(Notes.FirstOrDefault());
    }

    static string BuildImportMessage(UstImportResult result)
    {
        var parts = new List<string> { string.Format(Texts.UstImportedFormat, result.Notes.Count) };
        if (result.TrimmedRestTicks > 0)
            parts.Add(string.Format(Texts.UstRestTrimmedFormat, result.TrimmedRestTicks));
        if (result.TempoChangeCount > 0)
            parts.Add(Texts.UstTempoChangeIgnored);
        if (result.LegacyPitchNoteCount > 0)
            parts.Add(Texts.UstLegacyPitchIgnored);
        return string.Join("  ", parts);
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
        if (selectedNotes.Count == 0)
            return;

        foreach (var note in selectedNotes)
            note.Note.PitchPoints.Clear();
        SelectedPitchPoint = null;
        UpdatePitchCurve();
        UpdatePitchHandles();
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

        Batch(() =>
        {
            foreach (var note in Notes)
            {
                switch (SelectedExpression.NoteExpression)
                {
                    case NoteExpression.Velocity: note.Note.Velocity = 100.0; break;
                    case NoteExpression.Intensity: note.Note.Intensity = 100.0; break;
                    case NoteExpression.Modulation: note.Note.Modulation = 0.0; break;
                }
            }
        });
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

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
    public const double MinimumCurveSpacing = 1.0;
    public const double WindowMarginRatio = 0.5;

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
    double[] pitchSampleTicks = [];
    double[] pitchSampleTones = [];
    UTAUNote[] noteBuffer = [];
    int pitchSampleCount;
    double viewportLeft;
    double viewportWidth;
    int windowStartTicks;
    int windowEndTicks = int.MaxValue;
    int visibleFirst;
    int visibleLast = -1;

    public NoteEditorViewModel(UTAUVoicePronounce pronounce)
    {
        this.pronounce = pronounce;
        source = pronounce.Notes;
        Notes = [.. source.Select(x => new NoteViewModel(x, this))];
        Notes.CollectionChanged += OnNotesChanged;
        ZoomInCommand = new ActionCommand(_ => pixelsPerTick < MaximumPixelsPerTick, _ => ZoomHorizontally(ZoomStep));
        ZoomOutCommand = new ActionCommand(_ => pixelsPerTick > MinimumPixelsPerTick, _ => ZoomHorizontally(1.0 / ZoomStep));
        ZoomVerticalInCommand = new ActionCommand(_ => semitoneHeight < MaximumSemitoneHeight, _ => ZoomVertically(ZoomStep));
        ZoomVerticalOutCommand = new ActionCommand(_ => semitoneHeight > MinimumSemitoneHeight, _ => ZoomVertically(1.0 / ZoomStep));
        InsertRestCommand = new ActionCommand(_ => selectedNotes.Count > 0, _ => InsertRest());
        RemoveNoteCommand = new ActionCommand(_ => selectedNotes.Count > 0 && Notes.Count > selectedNotes.Count, _ => RemoveSelected());
        SelectAllCommand = new ActionCommand(_ => Notes.Count > 0, _ => SelectAll());
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

    public ObservableCollection<NoteViewModel> VisibleNotes { get; } = [];

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand ZoomVerticalInCommand { get; }

    public ICommand ZoomVerticalOutCommand { get; }

    public ICommand InsertRestCommand { get; }

    public ICommand RemoveNoteCommand { get; }

    public ICommand SelectAllCommand { get; }

    public string ImportMessage => pronounce.ImportMessage;

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

    public ObservableCollection<ExpressionBarViewModel> ExpressionBars { get; } = [];

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

    public int TotalTicks { get; private set; }

    public ObservableCollection<KeyRowViewModel> Keyboard { get; } = [];

    public ObservableCollection<GridLineViewModel> TimeGridLines { get; } = [];

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

        TotalTicks = position;
        OnPropertyChanged(nameof(TotalTicks));
        ResetWindow();
        UpdateKeyboard();
        UpdateTimeGridLines();
        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        UpdatePitchCurve();
        UpdatePitchHandles();
        UpdateExpression();
    }

    public void SetViewport(double left, double width)
    {
        viewportLeft = left;
        viewportWidth = width;
        if (!MoveWindow())
            return;

        SyncVisibleNotes();
        UpdateTimeGridLines();
        ProjectPitchCurve();
        UpdateExpression();
    }

    void OnNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        visibleFirst = 0;
        visibleLast = -1;
    }

    void ResetWindow()
    {
        windowStartTicks = int.MaxValue;
        windowEndTicks = int.MinValue;
        MoveWindow();
        SyncVisibleNotes();
    }

    bool MoveWindow()
    {
        int start;
        int end;
        if (viewportWidth <= 0.0 || PixelsPerTick <= 0.0)
        {
            start = 0;
            end = int.MaxValue;
        }
        else
        {
            var margin = viewportWidth * WindowMarginRatio;
            start = (int)Math.Max((viewportLeft - margin) / PixelsPerTick, 0.0);
            end = (int)Math.Min((viewportLeft + viewportWidth + margin) / PixelsPerTick, int.MaxValue);
        }

        if (start == windowStartTicks && end == windowEndTicks)
            return false;

        windowStartTicks = start;
        windowEndTicks = end;
        return true;
    }

    void SyncVisibleNotes()
    {
        var first = 0;
        var last = -1;
        for (var index = 0; index < Notes.Count; index++)
        {
            var note = Notes[index];
            if (note.EndTicks < windowStartTicks)
                continue;
            if (note.StartTicks > windowEndTicks)
                break;
            if (last < 0)
                first = index;
            last = index;
        }

        if (last < first)
        {
            VisibleNotes.Clear();
            visibleFirst = 0;
            visibleLast = -1;
            return;
        }

        if (!CanSlideTo(first, last))
        {
            VisibleNotes.Clear();
            for (var index = first; index <= last; index++)
                VisibleNotes.Add(Notes[index]);
            visibleFirst = first;
            visibleLast = last;
            return;
        }

        while (visibleFirst < first)
        {
            VisibleNotes.RemoveAt(0);
            visibleFirst++;
        }

        while (visibleLast > last)
        {
            VisibleNotes.RemoveAt(VisibleNotes.Count - 1);
            visibleLast--;
        }

        while (visibleFirst > first)
        {
            visibleFirst--;
            VisibleNotes.Insert(0, Notes[visibleFirst]);
        }

        while (visibleLast < last)
        {
            visibleLast++;
            VisibleNotes.Add(Notes[visibleLast]);
        }
    }

    public void InvalidateTones()
    {
        UpdateToneRange();
        foreach (var note in Notes)
            note.RaiseLayoutChanged();

        UpdateKeyboard();
        OnPropertyChanged(nameof(CanvasHeight));
        UpdatePitchCurve();
        UpdatePitchHandles();
    }

    public void InvalidateScale()
    {
        foreach (var note in Notes)
            note.RaiseLayoutChanged();

        if (MoveWindow())
            SyncVisibleNotes();
        UpdateKeyboard();
        UpdateTimeGridLines();
        OnPropertyChanged(nameof(CanvasWidth));
        OnPropertyChanged(nameof(CanvasHeight));
        ProjectPitchCurve();
        UpdatePitchHandles();
        UpdateExpression();
    }

    static void Resize<T>(ObservableCollection<T> collection, int count, Func<T> create)
    {
        if (count <= 0)
        {
            collection.Clear();
            return;
        }

        while (collection.Count > count)
            collection.RemoveAt(collection.Count - 1);
        while (collection.Count < count)
            collection.Add(create());
    }

    public void OnNoteChanged(string? propertyName)
    {
        if (isBatching)
            return;

        switch (propertyName)
        {
            case nameof(UTAUNote.Tone):
                InvalidateTones();
                break;
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
        var count = Notes.Count;
        if (noteBuffer.Length < count)
            noteBuffer = new UTAUNote[count];

        var required = 0;
        for (var index = 0; index < count; index++)
        {
            var note = Notes[index];
            noteBuffer[index] = note.Note;
            if (!note.IsRest)
                required += note.Note.LengthTicks / PitchCurveIntervalTicks + 1;
        }

        if (pitchSampleTicks.Length < required)
        {
            pitchSampleTicks = new double[required];
            pitchSampleTones = new double[required];
        }

        var tempoMap = TempoMap.Create(new ArraySegment<UTAUNote>(noteBuffer, 0, count), Time);
        var written = 0;
        var position = 0;

        for (var index = 0; index < count; index++)
        {
            var note = Notes[index];
            var length = note.Note.LengthTicks;
            if (note.IsRest)
            {
                position += length;
                continue;
            }

            var lengthMilliseconds = tempoMap.LengthMilliseconds(index);
            for (var elapsed = 0; elapsed <= length; elapsed += PitchCurveIntervalTicks)
            {
                var cents = note.Note.EvaluatePitchOffsetCents(elapsed / (double)length, lengthMilliseconds);
                pitchSampleTicks[written] = position + elapsed;
                pitchSampleTones[written] = note.Note.Tone + cents / 100.0;
                written++;
            }

            position += length;
        }

        pitchSampleCount = written;
        ProjectPitchCurve();
    }

    int LowerBound(int ticks)
    {
        var low = 0;
        var high = pitchSampleCount;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (pitchSampleTicks[middle] < ticks)
                low = middle + 1;
            else
                high = middle;
        }
        return Math.Max(low - 1, 0);
    }

    int UpperBound(int ticks)
    {
        var low = 0;
        var high = pitchSampleCount;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (pitchSampleTicks[middle] <= ticks)
                low = middle + 1;
            else
                high = middle;
        }
        return Math.Min(low + 1, pitchSampleCount);
    }

    void ProjectPitchCurve()
    {
        var spacing = PitchCurveIntervalTicks * PixelsPerTick;
        var step = spacing <= 0.0 ? 1 : Math.Max((int)(MinimumCurveSpacing / spacing), 1);
        var from = LowerBound(windowStartTicks);
        var to = UpperBound(windowEndTicks);
        var span = Math.Max(to - from, 0);
        var points = new PointCollection(step == 1 ? span : span / step * 2 + 2);

        if (step == 1)
        {
            for (var index = from; index < to; index++)
                points.Add(ToCanvasPoint(pitchSampleTicks[index], pitchSampleTones[index]));
        }
        else
        {
            for (var start = from; start < to; start += step)
            {
                var end = Math.Min(start + step, to);
                var lowest = start;
                var highest = start;
                for (var index = start + 1; index < end; index++)
                {
                    if (pitchSampleTones[index] < pitchSampleTones[lowest])
                        lowest = index;
                    if (pitchSampleTones[index] > pitchSampleTones[highest])
                        highest = index;
                }

                var first = Math.Min(lowest, highest);
                var second = Math.Max(lowest, highest);
                points.Add(ToCanvasPoint(pitchSampleTicks[first], pitchSampleTones[first]));
                if (second != first)
                    points.Add(ToCanvasPoint(pitchSampleTicks[second], pitchSampleTones[second]));
            }
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
            ExpressionBars.Clear();
            ExpressionCurvePoints = BuildExpressionCurve(workingCurve);
            return;
        }

        ExpressionCurvePoints = [];
        UpdateExpressionBars();
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
        Batch(
            () =>
            {
                for (var index = 0; index < transformTargets.Count; index++)
                    transformTargets[index].Note.PreviewTone(transformOriginTones[index] + shift);
            },
            InvalidateTones);
    }

    public void TransformLengths(int deltaTicks)
    {
        if (transformTargets.Count == 0)
            return;

        Batch(() =>
        {
            for (var index = 0; index < transformTargets.Count; index++)
                transformTargets[index].Note.PreviewLength(SnapLength(transformOriginLengths[index] + deltaTicks));
        });
    }

    public void EndTransform()
    {
        Batch(() =>
        {
            for (var index = 0; index < transformTargets.Count; index++)
            {
                var note = transformTargets[index].Note;
                var tone = note.Tone;
                var length = note.LengthTicks;

                if (tone != transformOriginTones[index])
                {
                    note.PreviewTone(transformOriginTones[index]);
                    note.Tone = tone;
                }

                if (length != transformOriginLengths[index])
                {
                    note.PreviewLength(transformOriginLengths[index]);
                    note.LengthTicks = length;
                }
            }
        });

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
        InvalidateScale();
    }

    public void ZoomVertically(double factor)
    {
        isAutoFitEnabled = false;
        SemitoneHeight = Math.Clamp(SemitoneHeight * factor, MinimumSemitoneHeight, MaximumSemitoneHeight);
        InvalidateScale();
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
        InvalidateScale();
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
        Notes.CollectionChanged -= OnNotesChanged;
        VisibleNotes.Clear();
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

    void Batch(Action action, Action? refresh = null)
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

        (refresh ?? InvalidateLayout)();
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
        var total = Math.Max(TotalTicks, ExpressionCurve.IntervalTicks);
        var first = Math.Max(windowStartTicks / ExpressionCurve.IntervalTicks - 1, 0);
        var last = Math.Min(windowEndTicks / ExpressionCurve.IntervalTicks + 1, total / ExpressionCurve.IntervalTicks);
        var spacing = ExpressionCurve.IntervalTicks * PixelsPerTick;
        var step = spacing <= 0.0 ? 1 : Math.Max((int)(MinimumCurveSpacing / spacing), 1);
        var span = Math.Max(last - first + 1, 0);
        var points = new PointCollection(step == 1 ? Math.Max(span, 1) : span / step * 2 + 2);

        if (step == 1)
        {
            for (var index = first; index <= last; index++)
                points.Add(ToStripPoint(values, expression, index));
        }
        else
        {
            for (var start = first; start <= last; start += step)
            {
                var end = Math.Min(start + step - 1, last);
                var lowest = start;
                var highest = start;
                for (var index = start + 1; index <= end; index++)
                {
                    if (SampleAt(values, index) < SampleAt(values, lowest))
                        lowest = index;
                    if (SampleAt(values, index) > SampleAt(values, highest))
                        highest = index;
                }

                var near = Math.Min(lowest, highest);
                var far = Math.Max(lowest, highest);
                points.Add(ToStripPoint(values, expression, near));
                if (far != near)
                    points.Add(ToStripPoint(values, expression, far));
            }
        }

        points.Freeze();
        return points;
    }

    static double SampleAt(double[] values, int index)
        => values.Length == 0 ? 0.0 : values[Math.Clamp(index, 0, values.Length - 1)];

    Point ToStripPoint(double[] values, ExpressionItem expression, int index)
        => new(
            index * ExpressionCurve.IntervalTicks * PixelsPerTick,
            (1.0 - expression.ToRatio(SampleAt(values, index))) * StripHeight);

    void UpdateExpressionBars()
    {
        var expression = SelectedExpression;
        var baseline = (1.0 - expression.Baseline) * StripHeight;
        var sung = 0;
        foreach (var note in Notes)
        {
            if (!note.IsRest && IsInsideWindow(note))
                sung++;
        }

        Resize(ExpressionBars, sung, () => new ExpressionBarViewModel());

        var index = 0;
        foreach (var note in Notes)
        {
            if (note.IsRest || !IsInsideWindow(note))
                continue;

            var value = expression.NoteExpression switch
            {
                NoteExpression.Velocity => note.Note.Velocity,
                NoteExpression.Modulation => note.Note.Modulation,
                _ => note.Note.Intensity,
            };
            var y = (1.0 - expression.ToRatio(value)) * StripHeight;
            var bar = ExpressionBars[index++];
            bar.Note = note;
            bar.Left = note.Left;
            bar.Width = note.Width;
            bar.Top = Math.Min(y, baseline);
            bar.Height = Math.Max(Math.Abs(baseline - y), 1.0);
        }
    }

    bool CanSlideTo(int first, int last)
        => visibleLast >= visibleFirst
            && last >= visibleFirst
            && first <= visibleLast
            && VisibleNotes.Count == visibleLast - visibleFirst + 1
            && visibleLast < Notes.Count
            && ReferenceEquals(VisibleNotes[0], Notes[visibleFirst])
            && ReferenceEquals(VisibleNotes[^1], Notes[visibleLast]);

    bool IsInsideWindow(NoteViewModel note)
        => note.EndTicks >= windowStartTicks && note.StartTicks <= windowEndTicks;

    void UpdateKeyboard()
    {
        var count = MaximumTone - MinimumTone + 1;
        Resize(Keyboard, count, () => new KeyRowViewModel());

        var width = CanvasWidth;
        var height = SemitoneHeight;
        for (var index = 0; index < count; index++)
        {
            var noteNumber = MaximumTone - index;
            var name = new MusicalTone(noteNumber).Name;
            var row = Keyboard[index];
            row.Name = name;
            row.IsAccidental = name.Contains('#');
            row.NoteNumber = noteNumber;
            row.Height = height;
            row.RollWidth = width;
        }
    }

    void UpdateTimeGridLines()
    {
        var total = TotalTicks;
        var last = total <= 1 ? 0 : (total - 1) / TimeBase.TicksPerQuarterNote;
        var first = Math.Max(windowStartTicks / TimeBase.TicksPerQuarterNote, 1);
        last = Math.Min(last, windowEndTicks / TimeBase.TicksPerQuarterNote);
        var count = Math.Max(last - first + 1, 0);
        Resize(TimeGridLines, count, () => new GridLineViewModel());

        var height = CanvasHeight;
        for (var index = 0; index < count; index++)
        {
            var ticks = (first + index) * TimeBase.TicksPerQuarterNote;
            var line = TimeGridLines[index];
            line.Left = ticks * PixelsPerTick;
            line.Height = height;
            line.IsBar = ticks % TimeBase.TicksPerWholeNote == 0;
        }
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

        Batch(
            () =>
            {
                foreach (var note in selectedNotes)
                    note.Tone += shift;
            },
            InvalidateTones);
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
        {
            var points = note.Note.PitchPoints;
            for (var index = points.Count - 1; index >= 0; index--)
                points.RemoveAt(index);
        }

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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace UTAU.Views;

internal sealed class ScrollingText : Control
{
    public const double PixelsPerSecond = 40.0;
    public const double HoldSeconds = 2.0;

    readonly TextBlock block = new();
    readonly TranslateTransform offset = new();

    public ScrollingText()
    {
        block.RenderTransform = offset;
        AddVisualChild(block);
        AddLogicalChild(block);
        ClipToBounds = true;
        SizeChanged += (_, _) => Restart();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(ScrollingText),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double HiddenWidth => Math.Max(block.DesiredSize.Width - ActualWidth, 0.0);

    public bool IsScrolling { get; private set; }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => block;

    static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollingText control)
            return;

        control.block.Text = e.NewValue as string ?? string.Empty;
        control.InvalidateMeasure();
        control.Restart();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        block.FontFamily = FontFamily;
        block.FontSize = FontSize;
        block.FontStyle = FontStyle;
        block.FontWeight = FontWeight;
        block.FontStretch = FontStretch;
        block.Foreground = Foreground;
        block.Measure(new Size(double.PositiveInfinity, constraint.Height));

        return new Size(
            double.IsInfinity(constraint.Width) ? block.DesiredSize.Width : Math.Min(block.DesiredSize.Width, constraint.Width),
            block.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size size)
    {
        block.Arrange(new Rect(0.0, 0.0, block.DesiredSize.Width, size.Height));
        return size;
    }

    void Restart()
    {
        offset.BeginAnimation(TranslateTransform.XProperty, null);
        offset.X = 0.0;
        IsScrolling = false;

        var hidden = HiddenWidth;
        if (hidden <= 1.0 || ActualWidth <= 0.0)
            return;

        var travel = TimeSpan.FromSeconds(hidden / PixelsPerSecond);
        var hold = TimeSpan.FromSeconds(HoldSeconds);
        var frames = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            KeyFrames =
            {
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(hold)),
                new LinearDoubleKeyFrame(-hidden, KeyTime.FromTimeSpan(hold + travel)),
                new LinearDoubleKeyFrame(-hidden, KeyTime.FromTimeSpan(hold + travel + hold)),
                new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(hold + travel + hold + travel)),
            },
        };

        IsScrolling = true;
        offset.BeginAnimation(TranslateTransform.XProperty, frames);
    }
}

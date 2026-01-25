using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Video_Size_Optimizer.Controls;

public class TimeSegmentControl : Control
{
    private Point _lastMousePosition;
    private bool _isDragging;

    // -------------------------
    // Styled Properties
    // -------------------------

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<TimeSegmentControl>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextBlock.FontSizeProperty.AddOwner<TimeSegmentControl>();

    public static readonly StyledProperty<FontStyle> FontStyleProperty =
        TextBlock.FontStyleProperty.AddOwner<TimeSegmentControl>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextBlock.FontWeightProperty.AddOwner<TimeSegmentControl>();

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontStyle FontStyle
    {
        get => GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<TimeSegmentControl, int>(
            nameof(Value),
            defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<TimeSegmentControl, int>(
            nameof(Max),
            defaultValue: 59);

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<TimeSegmentControl, string>(
            nameof(Format),
            defaultValue: "00");

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<TimeSegmentControl, IBrush?>(
            nameof(Foreground),
            Brushes.White);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<TimeSegmentControl, IBrush?>(
            nameof(Background));

    // -------------------------
    // CLR Wrappers
    // -------------------------

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    // -------------------------
    // Static Constructor
    // -------------------------

    static TimeSegmentControl()
    {
        FocusableProperty.OverrideDefaultValue<TimeSegmentControl>(true);

        AffectsRender<TimeSegmentControl>(
            ValueProperty,
            FormatProperty,
            ForegroundProperty,
            FontFamilyProperty,
            FontSizeProperty,
            FontStyleProperty,
            FontWeightProperty
        );

        AffectsMeasure<TimeSegmentControl>(
            ValueProperty,
            FormatProperty,
            FontFamilyProperty,
            FontSizeProperty,
            FontWeightProperty
        );

        ValueProperty.Changed.AddClassHandler<TimeSegmentControl>((ctrl, _) =>
        {
            ctrl.InvalidateMeasure();
            ctrl.InvalidateVisual();
        });
    }

    // -------------------------
    // Layout
    // -------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = CreateFormattedText();
        return new Size(
            Math.Ceiling(text.Width) + 6,
            Math.Ceiling(text.Height) + 4);
    }

    // -------------------------
    // Rendering
    // -------------------------

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;

        if (Background != null)
        {
            context.FillRectangle(Background, bounds);
        }

        var text = CreateFormattedText();

        var x = (bounds.Width - text.Width) / 2;
        var y = (bounds.Height - text.Height) / 2;

        context.DrawText(text, new Point(x, y));
    }

    private FormattedText CreateFormattedText()
    {
        return new FormattedText(
            Value.ToString(Format),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight),
            FontSize,
            Foreground ?? Brushes.White);
    }

    // -------------------------
    // Interaction
    // -------------------------

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        UpdateValue(e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging)
            return;

        var pos = e.GetPosition(this);
        var deltaY = _lastMousePosition.Y - pos.Y;

        if (Math.Abs(deltaY) >= 5)
        {
            UpdateValue(deltaY > 0 ? 1 : -1);
            _lastMousePosition = pos;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void UpdateValue(int delta)
    {
        var newValue = Value + delta;

        if (newValue > Max)
            newValue = 0;
        else if (newValue < 0)
            newValue = Max;

        Value = newValue;
    }
}

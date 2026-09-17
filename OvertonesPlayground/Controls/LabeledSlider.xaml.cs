namespace OvertonesPlayground.Controls;

/// <summary>
/// A label + slider + numeric entry, kept in sync: dragging the slider updates the entry text
/// and vice versa. The entry gets a <c>NumericValidationBehavior</c> so out-of-range or
/// non-numeric typed input is visibly flagged rather than silently accepted.
/// </summary>
public partial class LabeledSlider : ContentView
{
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(nameof(LabelText), typeof(string), typeof(LabeledSlider), string.Empty);

    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(nameof(Minimum), typeof(double), typeof(LabeledSlider), 0.0);

    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(nameof(Maximum), typeof(double), typeof(LabeledSlider), 1.0);

    public static readonly BindableProperty DigitsProperty =
        BindableProperty.Create(nameof(Digits), typeof(int), typeof(LabeledSlider), 2);

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(LabeledSlider), 0.0, BindingMode.TwoWay, propertyChanged: OnValueChanged);

    public LabeledSlider()
    {
        InitializeComponent();
    }

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>How many decimal places the entry accepts and displays.</summary>
    public int Digits
    {
        get => (int)GetValue(DigitsProperty);
        set => SetValue(DigitsProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((LabeledSlider)bindable).SyncEntryText();

    private void SyncEntryText()
    {
        var formatted = Value.ToString($"F{Digits}");
        if (ValueEntry.Text != formatted)
        {
            ValueEntry.Text = formatted;
        }
    }

    private void OnEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (double.TryParse(e.NewTextValue, out var parsed) && parsed >= Minimum && parsed <= Maximum)
        {
            Value = parsed;
        }
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is not null)
        {
            SyncEntryText();
        }
    }
}

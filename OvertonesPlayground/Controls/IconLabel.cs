namespace OvertonesPlayground.Controls;

/// <summary>
/// A label with a Material Symbols icon in front of its text, for headings such as "Pitch Sweep". The icon and the text share
/// one <see cref="Label"/> (two spans in a formatted string), so the row keeps a single accessibility node and a single
/// <c>AutomationId</c>. Use <see cref="Caption"/> instead of <c>Text</c>: a label shows either its text or a formatted string,
/// and this control needs the formatted one. Give it an explicit <c>Style</c>, as an implicit <c>Label</c> style does not reach
/// a derived type. The glyph is a private-use character no screen reader can pronounce, so the caption is also set as the
/// control's <c>SemanticProperties.Description</c>.
/// </summary>
public class IconLabel : Label
{
    #region Constants
    // The icon is drawn a little larger than the text so it reads at the same visual weight.
    private const double IconScale = 1.3;

    // An en space: wider than a word space, so the icon does not run into the text.
    private const string Gap = " ";
    #endregion

    #region Bindable properties
    /// <summary>The text shown after the icon.</summary>
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(nameof(Caption), typeof(string), typeof(IconLabel), string.Empty, propertyChanged: OnContentChanged);

    /// <summary>The <c>IconFont</c> glyph shown before the caption.</summary>
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(nameof(Glyph), typeof(string), typeof(IconLabel), string.Empty, propertyChanged: OnContentChanged);
    #endregion

    #region Properties
    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
    #endregion

    #region Private methods
    private static void OnContentChanged(BindableObject bindable, object oldValue, object newValue) => ((IconLabel)bindable).Rebuild();

    private void Rebuild()
    {
        FormattedString content = new();
        content.Spans.Add(new Span { Text = Glyph, FontFamily = Icon.FontFamily, FontSize = FontSize * IconScale });
        content.Spans.Add(new Span { Text = Gap + Caption });
        FormattedText = content;
        SemanticProperties.SetDescription(this, Caption);
    }
    #endregion

    #region Protected methods
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // The icon span is sized from the label's font size, which arrives late, when the style is applied.
        if (propertyName == FontSizeProperty.PropertyName)
        {
            Rebuild();
        }
    }
    #endregion
}

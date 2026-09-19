using OvertonesPlayground.Themes;

namespace OvertonesPlayground.Controls;

/// <summary>
/// Puts a Material Symbols icon on a <see cref="Button"/> or a <see cref="ToolbarItem"/> without giving up the control's own
/// look: <c>controls:Icon.Glyph="{x:Static models:IconFont.Play_arrow}"</c>. Neither control can mix fonts inside its text, so the
/// icon is drawn as a <see cref="FontImageSource"/> beside (or instead of) the text. A font image has one fixed color, so the
/// icon is redrawn whenever the button's <see cref="Button.TextColor"/> changes; that keeps it matching the label through
/// pressed/disabled states, toggle triggers and light/dark switches. A toolbar item has no text color, so it follows the
/// <c>TextFillColorPrimary</c> token through a dynamic resource unless <see cref="ColorProperty"/> says otherwise.
/// </summary>
public static class Icon
{
    #region Constants
    /// <summary>The alias the Material Symbols font is registered under in <c>MauiProgram</c>.</summary>
    public const string FontFamily = "MaterialSymbols";

    private const double DefaultSize = 18;
    private const double GapToText = 8;
    private const string ToolbarColorToken = "TextFillColorPrimary";
    #endregion

    #region Bindable properties
    /// <summary>The icon's <c>IconFont</c> glyph. Clear it to remove the icon.</summary>
    public static readonly BindableProperty GlyphProperty = BindableProperty.CreateAttached("Glyph", typeof(string), typeof(Icon), null, propertyChanged: OnIconChanged);

    /// <summary>The icon's size in device-independent units.</summary>
    public static readonly BindableProperty SizeProperty = BindableProperty.CreateAttached("Size", typeof(double), typeof(Icon), DefaultSize, propertyChanged: OnIconChanged);

    /// <summary>An explicit icon color. Without it a button's icon takes the button's text color.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.CreateAttached("Color", typeof(Color), typeof(Icon), null, propertyChanged: OnIconChanged);
    #endregion

    #region Private methods
    private static FontImageSource Create(string glyph, double size, Color color) => new() { FontFamily = FontFamily, Glyph = glyph, Size = size, Color = color };

    private static bool IsCurrent(ImageSource? current, string glyph, double size, Color color) => current is FontImageSource font && font.Glyph == glyph && font.Size == size && font.Color.Equals(color);

    private static void OnButtonPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is Button button && e.PropertyName == Button.TextColorProperty.PropertyName)
        {
            Refresh(button);
        }
    }

    private static void OnIconChanged(BindableObject bindable, object oldValue, object newValue)
    {
        switch (bindable)
        {
            case Button button:
                // Unsubscribing first keeps it to one subscription however many of the icon's properties are set.
                button.PropertyChanged -= OnButtonPropertyChanged;
                button.PropertyChanged += OnButtonPropertyChanged;
                Refresh(button);
                break;
            case ToolbarItem item:
                if (!item.IsSet(ColorProperty))
                {
                    item.SetDynamicResource(ColorProperty, ToolbarColorToken);
                }

                // With only an icon showing, the item's text is what a screen reader should announce. Without this Android
                // announces the AutomationId ("library-import"). It is bound, so a text that changes (Edit / Done) follows.
                if (!item.IsSet(SemanticProperties.DescriptionProperty))
                {
                    item.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(ToolbarItem.Text), source: item));
                }

                Refresh(item);
                break;
            default:
                break;
        }
    }

    private static void Refresh(Button button)
    {
        string? glyph = GetGlyph(button);
        if (string.IsNullOrEmpty(glyph))
        {
            button.ImageSource = null;
            return;
        }

        Color color = GetColor(button) ?? button.TextColor ?? FluentTheme.GetColor(ToolbarColorToken);
        double size = GetSize(button);
        if (IsCurrent(button.ImageSource, glyph, size, color))
        {
            return;
        }

        if (!button.IsSet(Button.ContentLayoutProperty))
        {
            button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, GapToText);
        }

        button.ImageSource = Create(glyph, size, color);
    }

    private static void Refresh(ToolbarItem item)
    {
        string? glyph = GetGlyph(item);
        if (string.IsNullOrEmpty(glyph))
        {
            item.IconImageSource = null;
            return;
        }

        Color color = GetColor(item) ?? FluentTheme.GetColor(ToolbarColorToken);
        double size = GetSize(item);
        if (!IsCurrent(item.IconImageSource, glyph, size, color))
        {
            item.IconImageSource = Create(glyph, size, color);
        }
    }
    #endregion

    #region Public methods
    public static Color? GetColor(BindableObject view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return (Color?)view.GetValue(ColorProperty);
    }

    public static string? GetGlyph(BindableObject view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return (string?)view.GetValue(GlyphProperty);
    }

    public static double GetSize(BindableObject view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return (double)view.GetValue(SizeProperty);
    }

    public static void SetColor(BindableObject view, Color? value)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.SetValue(ColorProperty, value);
    }

    public static void SetGlyph(BindableObject view, string? value)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.SetValue(GlyphProperty, value);
    }

    public static void SetSize(BindableObject view, double value)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.SetValue(SizeProperty, value);
    }
    #endregion
}

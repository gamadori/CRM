namespace CRM.Mobile.Pages;

/// <summary>
/// Aspetto condiviso delle pagine. Sta in un posto solo perche' l'app e' fatta di due schermate
/// che devono sembrare la stessa cosa, e ripetere i colori a mano le fa divergere alla prima
/// modifica.
/// </summary>
internal static class Theme
{
    public static readonly Color Brand = Color.FromArgb("#0F766E");
    public static readonly Color Page = Color.FromArgb("#F7F8FA");
    public static readonly Color Line = Color.FromArgb("#DCE3E8");
    public static readonly Color Muted = Color.FromArgb("#65727D");
    public static readonly Color Ok = Color.FromArgb("#16A34A");
    public static readonly Color Warn = Color.FromArgb("#B45309");
    public static readonly Color Danger = Color.FromArgb("#DC2626");

    public static Label SectionTitle(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontAttributes = FontAttributes.Bold,
        FontSize = 12,
        TextColor = Muted,
        Margin = new Thickness(2, 10, 2, 0)
    };

    public static Label FieldLabel(string text) => new()
    {
        Text = text,
        FontAttributes = FontAttributes.Bold,
        FontSize = 13,
        TextColor = Muted
    };

    public static Label Hint(string text) => new()
    {
        Text = text,
        FontSize = 12.5,
        TextColor = Muted,
        LineBreakMode = LineBreakMode.WordWrap
    };

    /// <summary>Bottone principale: alto abbastanza da centrarlo in piedi, con una mano occupata.</summary>
    public static Button PrimaryButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Brand,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10,
            MinimumHeightRequest = 52
        };

        button.Clicked += async (_, _) => await action();
        return button;
    }

    public static Button SecondaryButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Colors.White,
            TextColor = Brand,
            BorderColor = Line,
            BorderWidth = 1,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10,
            MinimumHeightRequest = 46
        };

        button.Clicked += async (_, _) => await action();
        return button;
    }

    public static Border Card(params IView[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 10 };
        foreach (var child in children)
            stack.Children.Add(child);

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Line,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = 14,
            Content = stack
        };
    }
}

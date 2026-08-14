namespace AURA.Mobile.Pages;

// Mapa de ícone por label de seção/item
file static class SectionIcons
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Seções
        ["Sistema"]      = "⚙",
        ["Assistente"]   = "◈",
        ["Ferramentas"]  = "⬡",
        ["Apps"]         = "▣",
        // Itens
        ["Início"]           = "⌂",
        ["Logs"]             = "≡",
        ["Correções"]        = "⚕",
        ["Chat"]             = "◉",
        ["Agente"]           = "◆",
        ["Memória"]          = "⬟",
        ["Terminal"]         = ">_",
        ["Executores"]       = "▶",
        ["Módulos"]          = "⊞",
        ["Navegador"]        = "⊕",
        ["Células"]          = "⬡",
        ["Rodar programa"]   = "▷",
    };

    public static string Get(string label) =>
        Map.TryGetValue(label, out string? ico) ? ico : "◇";
}

/// <summary>
/// Menu de seção: grade 2×N de cards com ícone + label.
/// </summary>
public sealed class SectionPage : ContentPage
{
    private static readonly Color Bg        = Color.FromArgb("#0c0c12");
    private static readonly Color CardBg    = Color.FromArgb("#13131d");
    private static readonly Color CardStroke = Color.FromArgb("#242438");
    private static readonly Color AccentCol  = Color.FromArgb("#4f8aff");
    private static readonly Color TextPri   = Color.FromArgb("#e8e8f0");
    private static readonly Color TextSec   = Color.FromArgb("#7a7a90");

    public SectionPage(string title, params (string Label, Page Page)[] items)
    {
        Title = title;
        BackgroundColor = Bg;

        // Cabeçalho da seção
        var header = new VerticalStackLayout
        {
            Padding = new Thickness(20, 22, 20, 8),
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = SectionIcons.Get(title) + "  " + title.ToUpperInvariant(),
                    FontSize = 11,
                    TextColor = AccentCol,
                    FontAttributes = FontAttributes.Bold,
                },
                new Label
                {
                    Text = "AURA · " + items.Length + " opção" + (items.Length != 1 ? "ões" : ""),
                    FontSize = 12,
                    TextColor = TextSec,
                }
            }
        };

        // Grade 2 colunas
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 12,
            RowSpacing = 12,
            Padding = new Thickness(20, 0, 20, 24),
        };

        int col = 0, row = 0;
        foreach (var (label, page) in items)
        {
            if (col == 0)
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var card = MakeCard(label, page);
            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);
            grid.Children.Add(card);

            col++;
            if (col == 2) { col = 0; row++; }
        }

        // Se número ímpar, preenche célula vazia
        if (col == 1)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children = { header, grid }
            }
        };
    }

    private View MakeCard(string label, Page page)
    {
        var icon = new Label
        {
            Text = SectionIcons.Get(label),
            FontSize = 26,
            TextColor = AccentCol,
            HorizontalOptions = LayoutOptions.Center,
        };

        var lbl = new Label
        {
            Text = label,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextPri,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
        };

        var inner = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(12, 18),
            HorizontalOptions = LayoutOptions.Fill,
            Children = { icon, lbl }
        };

        var card = new Border
        {
            BackgroundColor = CardBg,
            Stroke = CardStroke,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Content = inner,
        };

        // Toque no card
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            // Micro feedback visual
            await card.ScaleTo(0.96, 80);
            await card.ScaleTo(1.0, 80);
            try { await Navigation.PushAsync(page); }
            catch (Exception ex) { AuraLog.Exception("SectionPage " + label, ex); }
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }
}

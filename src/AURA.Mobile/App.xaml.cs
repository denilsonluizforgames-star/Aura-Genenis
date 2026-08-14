using AURA.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AURA.Mobile;

public partial class App : Application
{
    private readonly AuraConfiguration _settings;
    private readonly IServiceProvider _services;
    private const string ThemePrefKey = "aura_theme";

    public App(IServiceProvider services, AuraConfiguration settings)
    {
        _services = services;
        _settings = settings;
        AuraLog.Info("App.ctor BEGIN");
        try
        {
            InitializeComponent();
            AuraLog.Info("App.ctor InitializeComponent OK");
            // false = Lunar (padrão), true = Solar
            IsSolar = Preferences.Default.Get(ThemePrefKey, false);
            ApplyColors();
            MainPage = _services.GetRequiredService<MainPage>();
            AuraLog.Info("App.ctor MainPage set OK (tema=" + (IsSolar ? "Solar" : "Lunar") + ")");
        }
        catch (Exception ex)
        {
            AuraLog.Exception("App.ctor", ex);
            throw;
        }
    }

    public static bool IsSolar { get; private set; }
    public static event Action? ThemeChanged;

    public static void ToggleTheme()
    {
        IsSolar = !IsSolar;
        Preferences.Default.Set(ThemePrefKey, IsSolar);
        ApplyColors();
        ThemeChanged?.Invoke();
        AuraLog.Info("Tema alterado para " + (IsSolar ? "Solar" : "Lunar"));
    }

    private static void ApplyColors()
    {
        var r = Current?.Resources;
        if (r is null) return;

        if (IsSolar)
        {
            // Solar — ciano / teal sobre fundo escuro (conceito holográfico diurno)
            SetColor(r, "AuraBackground", "#1c1a2e");
            SetColor(r, "AuraSurface", "#12101f");
            SetColor(r, "AuraSurface2", "#26233a");
            SetColor(r, "AuraAccent", "#5bcfeb");
            SetColor(r, "AuraAccentDim", "#0f2e3a");
            SetColor(r, "AuraAccentGlow", "#0a1f28");
            SetColor(r, "AuraAccent2", "#bd5ae0");
            SetColor(r, "AuraCyan", "#5bcfeb");
            SetColor(r, "AuraCyanDim", "#0f2e3a");
            SetColor(r, "AuraTextPrimary", "#f5f5f8");
            SetColor(r, "AuraTextSecondary", "#bdc2d0");
            SetColor(r, "AuraTextMuted", "#6b6f80");
            SetColor(r, "AuraSuccess", "#6cdb9a");
            SetColor(r, "AuraError", "#e05560");
            SetColor(r, "AuraWarning", "#f5b85a");
            SetColor(r, "AuraBorder", "#3a3750");
            SetColor(r, "AuraBorderAccent", "#3a8aa0");
            SetColor(r, "AuraUserBubble", "#1e3854");
            SetColor(r, "AuraAgentBubble", "#12101f");
            SetColor(r, "AuraToolBubble", "#0f1420");
            SetColor(r, "AuraGlass", "#9912101f");
            SetColor(r, "AuraGlassBorder", "#33ffffff");
        }
        else
        {
            // Lunar — azul frio / prata / violeta (padrão)
            SetColor(r, "AuraBackground", "#12141f");
            SetColor(r, "AuraSurface", "#0d0f18");
            SetColor(r, "AuraSurface2", "#1e2130");
            SetColor(r, "AuraAccent", "#7a9eff");
            SetColor(r, "AuraAccentDim", "#14224a");
            SetColor(r, "AuraAccentGlow", "#0e1a35");
            SetColor(r, "AuraAccent2", "#8a5ae0");
            SetColor(r, "AuraCyan", "#7a9eff");
            SetColor(r, "AuraCyanDim", "#14224a");
            SetColor(r, "AuraTextPrimary", "#eef0f5");
            SetColor(r, "AuraTextSecondary", "#b8bcc8");
            SetColor(r, "AuraTextMuted", "#5a5e70");
            SetColor(r, "AuraSuccess", "#6cdb9a");
            SetColor(r, "AuraError", "#e05560");
            SetColor(r, "AuraWarning", "#f5b85a");
            SetColor(r, "AuraBorder", "#2a2d40");
            SetColor(r, "AuraBorderAccent", "#2a3a6a");
            SetColor(r, "AuraUserBubble", "#1e2d54");
            SetColor(r, "AuraAgentBubble", "#0d0f18");
            SetColor(r, "AuraToolBubble", "#0f1420");
            SetColor(r, "AuraGlass", "#990d0f18");
            SetColor(r, "AuraGlassBorder", "#33ffffff");
        }
    }

    private static void SetColor(ResourceDictionary r, string key, string hex)
    {
        if (r.ContainsKey(key))
            r[key] = Color.FromArgb(hex);
        else
            r.Add(key, Color.FromArgb(hex));
    }

    protected override void OnStart()
    {
        base.OnStart();
        AuraLog.Info("App.OnStart");
    }

    protected override void OnSleep()
    {
        AuraLog.Info("App.OnSleep");
        base.OnSleep();
    }

    protected override void OnResume()
    {
        base.OnResume();
        AuraLog.Info("App.OnResume");
    }
}

using AURA.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AURA.Mobile;

public partial class App : Application
{
    private readonly AuraConfiguration _settings;
    private readonly IServiceProvider _services;

    public App(IServiceProvider services, AuraConfiguration settings)
    {
        _services = services;
        _settings = settings;
        AuraLog.Info("App.ctor BEGIN");
        try
        {
            InitializeComponent();
            AuraLog.Info("App.ctor InitializeComponent OK");
            UserAppTheme = ApplyTheme(_settings?.Theme);
            MainPage = _services.GetRequiredService<MainPage>();
            AuraLog.Info("App.ctor MainPage set OK");
        }
        catch (Exception ex)
        {
            AuraLog.Exception("App.ctor", ex);
            throw;
        }
    }

    private static AppTheme ApplyTheme(string theme)
    {
        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return AppTheme.Dark;
        }

        if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return AppTheme.Light;
        }

        return AppTheme.Unspecified;
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

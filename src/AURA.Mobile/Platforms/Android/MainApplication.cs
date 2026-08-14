using Android.App;
using Android.Runtime;

namespace AURA.Mobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
    {
        // O construtor do Application é o PRIMEIRO código gerenciado que roda no processo.
        // Logcat já funciona aqui; arquivo ainda não (sem Context útil) — é armazenado em buffer.
        AuraLog.Info("MainApplication.ctor");
    }

    public override void OnCreate()
    {
        // Contexto disponível: inicializa o arquivo de log e instala os handlers
        // globais ANTES de qualquer inicialização do MAUI.
        AuraLog.Init(this);
        AuraLog.WireGlobalExceptionHandlers();
        AuraLog.Info("MainApplication.OnCreate BEGIN");
        try
        {
            base.OnCreate();
            AuraLog.Info("MainApplication.OnCreate OK");
        }
        catch (Exception ex)
        {
            AuraLog.Exception("MainApplication.OnCreate", ex);
            throw;
        }
    }

    protected override MauiApp CreateMauiApp()
    {
        AuraLog.Info("CreateMauiApp BEGIN");
        try
        {
            MauiApp app = MauiProgram.CreateMauiApp();
            AuraLog.Info("CreateMauiApp OK");
            return app;
        }
        catch (Exception ex)
        {
            AuraLog.Exception("CreateMauiApp", ex);
            throw;
        }
    }
}

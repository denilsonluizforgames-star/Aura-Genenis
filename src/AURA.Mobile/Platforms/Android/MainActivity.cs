using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.Threading.Tasks;
using AURA.Mobile.Platforms.Android;

namespace AURA.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PickProjectTreeRequest = 4107;
    private TaskCompletionSource<Android.Net.Uri?>? _projectPicker;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AuraLog.Info("MainActivity.OnCreate BEGIN");
        try
        {
            base.OnCreate(savedInstanceState);
            AuraLog.Info("MainActivity.OnCreate OK");

            // Botão flutuante de voz sobre todas as abas (fala a última resposta).
            try
            {
                VoiceFloatingButton.Attach(this);
            }
            catch (Exception ex)
            {
                AuraLog.Exception("MainActivity.VoiceFloatingButton", ex);
            }
        }
        catch (Exception ex)
        {
            AuraLog.Exception("MainActivity.OnCreate", ex);
            throw;
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        AuraLog.Info("MainActivity.OnResume OK");
    }

    public Task<Android.Net.Uri?> PickProjectDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPicker != null)
            throw new InvalidOperationException("O seletor de projeto já está aberto.");

        _projectPicker = new TaskCompletionSource<Android.Net.Uri?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        cancellationToken.Register(() =>
        {
            _projectPicker?.TrySetCanceled(cancellationToken);
            _projectPicker = null;
        });

        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission |
                        ActivityFlags.GrantWriteUriPermission |
                        ActivityFlags.GrantPersistableUriPermission);
        StartActivityForResult(intent, PickProjectTreeRequest);
        return _projectPicker.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode != PickProjectTreeRequest)
            return;

        Android.Net.Uri? uri = resultCode == Result.Ok ? data?.Data : null;
        _projectPicker?.TrySetResult(uri);
        _projectPicker = null;
    }

    protected override void OnDestroy()
    {
        _projectPicker?.TrySetResult(null);
        _projectPicker = null;
        VoiceFloatingButton.Detach();
        AuraLog.Info("MainActivity.OnDestroy");
        base.OnDestroy();
    }
}

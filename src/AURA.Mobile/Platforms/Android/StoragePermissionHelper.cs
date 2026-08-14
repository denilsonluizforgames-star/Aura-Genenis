using Android.Content;
using Android.OS;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;

namespace AURA.Mobile;

public static class StoragePermissionHelper
{
    public static async Task<bool> EnsureStorageAccessAsync()
    {
        var permissions = new List<Permissions.BasePermission>();

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var media = new Permissions.Media();
            if (await media.CheckStatusAsync() != PermissionStatus.Granted)
                permissions.Add(media);
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            // Android 11+ (API 30+): WRITE_EXTERNAL_STORAGE é ignorada pelo sistema
            // (armazenamento com escopo) e não está declarada no manifest para esta
            // API — solicitar via Permissions.StorageWrite só gera o aviso
            // "You need to declare... WRITE_EXTERNAL_STORAGE". Escrita fora do SAF
            // não é concedida por permissão; o acesso a projetos é via SAF
            // (ProjectAccessService). Leitura usa READ_EXTERNAL_STORAGE.
            var read = new Permissions.StorageRead();
            if (await read.CheckStatusAsync() != PermissionStatus.Granted)
                permissions.Add(read);
        }
        else
        {
            var read = new Permissions.StorageRead();
            if (await read.CheckStatusAsync() != PermissionStatus.Granted)
                permissions.Add(read);
            var write = new Permissions.StorageWrite();
            if (await write.CheckStatusAsync() != PermissionStatus.Granted)
                permissions.Add(write);
        }

        foreach (var permission in permissions)
        {
            var status = await permission.RequestAsync();
            if (status != PermissionStatus.Granted)
                return false;
        }

        return true;
    }

    public static bool IsAllFilesAccessGranted()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            return Android.OS.Environment.IsExternalStorageManager;
        return true;
    }

    public static void RequestAllFilesAccess()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30) || Android.OS.Environment.IsExternalStorageManager)
            return;

        var activity = Platform.CurrentActivity;
        if (activity == null)
            return;

        var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission,
            Android.Net.Uri.Parse("package:" + activity.PackageName));
        activity.StartActivity(intent);
    }
}

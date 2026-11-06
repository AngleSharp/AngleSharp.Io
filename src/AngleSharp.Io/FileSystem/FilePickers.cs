namespace AngleSharp.Io.FileSystemApi;

using AngleSharp.Dom;
using AngleSharp.Io.Dom;
using AngleSharp.Io.PermissionsApi;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Runs the steps shared by the pickers on the window.
/// </summary>
static class FilePickers
{
    public static async Task<FileSystemFileHandle[]> ShowOpenFilePickerAsync(IWindow window, OpenFilePickerOptions options)
    {
        options ??= new OpenFilePickerOptions();
        var platform = Prepare(window, options);
        var permissions = Permissions.ForWindow(window);

        if (permissions != null)
        {
            await permissions.EnsureGrantedAsync(PermissionNames.FileSystemRead).ConfigureAwait(false);
        }

        var entries = await platform.PickFilesAsync(LockManager.GetOrigin(window), options).ConfigureAwait(false);

        if (entries is null || entries.Length == 0)
        {
            throw new DomException(DomError.Abort);
        }

        return entries
            .Take(options.Multiple ? entries.Length : 1)
            .Select(entry => new FileSystemFileHandle(platform, entry, permissions))
            .ToArray();
    }

    public static async Task<FileSystemFileHandle> ShowSaveFilePickerAsync(IWindow window, SaveFilePickerOptions options)
    {
        options ??= new SaveFilePickerOptions();
        var platform = Prepare(window, options);
        var permissions = Permissions.ForWindow(window);

        if (permissions != null)
        {
            await permissions.EnsureGrantedAsync(PermissionNames.FileSystemWrite).ConfigureAwait(false);
        }

        var entry = await platform.PickSaveFileAsync(LockManager.GetOrigin(window), options).ConfigureAwait(false);

        if (entry is null)
        {
            throw new DomException(DomError.Abort);
        }

        return new FileSystemFileHandle(platform, entry, permissions);
    }

    private static IFileSystemPlatform Prepare(IWindow window, FilePickerOptions options)
    {
        var context = window?.Document?.Context;
        var platform = context?.GetService<IFileSystemPlatform>() ?? throw new DomException(DomError.NotSupported);
        var requireSecureContext = context.GetService<FileSystemAccessOptions>()?.RequireSecureContext ?? true;

        options.Validate();

        if (requireSecureContext && !ServiceWorkerContainer.IsPotentiallyTrustworthy(new Url(window.Document.Url)))
        {
            throw new DomException(DomError.Security);
        }

        return platform;
    }
}

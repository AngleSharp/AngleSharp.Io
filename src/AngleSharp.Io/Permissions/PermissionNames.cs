namespace AngleSharp.Io.PermissionsApi;

using System;

/// <summary>
/// The names of the permissions known by default.
/// </summary>
public static class PermissionNames
{
    /// <summary>
    /// Reading the position via navigator.geolocation.
    /// </summary>
    public static readonly String Geolocation = "geolocation";

    /// <summary>
    /// Showing notifications.
    /// </summary>
    public static readonly String Notifications = "notifications";

    /// <summary>
    /// Reading from the clipboard via navigator.clipboard.
    /// </summary>
    public static readonly String ClipboardRead = "clipboard-read";

    /// <summary>
    /// Writing to the clipboard via navigator.clipboard.
    /// </summary>
    public static readonly String ClipboardWrite = "clipboard-write";

    /// <summary>
    /// Excluding the storage from eviction via navigator.storage.persist().
    /// </summary>
    public static readonly String PersistentStorage = "persistent-storage";

    /// <summary>
    /// Reading files via window.showOpenFilePicker() and their handles.
    /// Having <see cref="FileSystemWrite"/> granted includes reading.
    /// </summary>
    public static readonly String FileSystemRead = "file-system-read";

    /// <summary>
    /// Writing files via window.showSaveFilePicker() and their handles.
    /// </summary>
    public static readonly String FileSystemWrite = "file-system-write";

    /// <summary>
    /// Registering one-off tasks via ServiceWorkerRegistration.sync.
    /// </summary>
    public static readonly String BackgroundSync = "background-sync";

    /// <summary>
    /// Registering periodic tasks via ServiceWorkerRegistration.periodicSync.
    /// </summary>
    public static readonly String PeriodicBackgroundSync = "periodic-background-sync";

    /// <summary>
    /// Subscribing via ServiceWorkerRegistration.pushManager. Subscriptions
    /// with userVisibleOnly also need <see cref="Notifications"/>.
    /// </summary>
    public static readonly String Push = "push";

    internal static readonly String[] All = new[]
    {
        Geolocation,
        Notifications,
        ClipboardRead,
        ClipboardWrite,
        PersistentStorage,
        FileSystemRead,
        FileSystemWrite,
        BackgroundSync,
        PeriodicBackgroundSync,
        Push,
    };
}

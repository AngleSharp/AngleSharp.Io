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

    internal static readonly String[] All = new[]
    {
        Geolocation,
        Notifications,
        ClipboardRead,
        ClipboardWrite,
        PersistentStorage,
    };
}

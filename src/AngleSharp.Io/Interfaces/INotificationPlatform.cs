namespace AngleSharp.Io.Dom;

using System.Threading.Tasks;

/// <summary>
/// Represents the platform delivering notifications, e.g., to the desktop of
/// the host or to a test double. Whether notifications may be shown is not
/// up to the platform, but to the registered <see cref="IPermissionPolicy"/>.
/// </summary>
public interface INotificationPlatform
{
    /// <summary>
    /// Shows the given notification. A faulted task dispatches the error
    /// event on the notification. The platform reports interactions via
    /// <see cref="Notification.NotifyClicked"/> and
    /// <see cref="Notification.NotifyClosed"/>.
    /// </summary>
    /// <param name="notification">The notification to show.</param>
    /// <returns>The task completing once the notification is shown.</returns>
    Task ShowAsync(Notification notification);

    /// <summary>
    /// Removes the given notification, which has been closed by the script.
    /// </summary>
    /// <param name="notification">The notification to remove.</param>
    /// <returns>The task completing once the notification is removed.</returns>
    Task CloseAsync(Notification notification);
}

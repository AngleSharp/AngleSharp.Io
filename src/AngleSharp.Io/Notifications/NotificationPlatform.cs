namespace AngleSharp.Io.NotificationsApi;

using AngleSharp.Io.Dom;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents a notification platform delegating to optional callbacks.
/// Without callbacks, notifications are considered shown and removed without
/// anything being displayed.
/// </summary>
public sealed class NotificationPlatform : INotificationPlatform
{
    /// <summary>
    /// Gets or sets the callback invoked for showing a notification.
    /// </summary>
    public Func<Notification, Task> Show { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked for removing a notification.
    /// </summary>
    public Func<Notification, Task> Close { get; set; }

    /// <inheritdoc />
    public Task ShowAsync(Notification notification) => Show?.Invoke(notification) ?? Task.CompletedTask;

    /// <inheritdoc />
    public Task CloseAsync(Notification notification) => Close?.Invoke(notification) ?? Task.CompletedTask;
}

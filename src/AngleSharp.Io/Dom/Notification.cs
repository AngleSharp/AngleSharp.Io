namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Browser;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using AngleSharp.Io.PermissionsApi;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents the Notification interface. Delivering a notification is up to
/// the <see cref="INotificationPlatform"/>, the permission is decided by the
/// registered <see cref="IPermissionPolicy"/>; without a policy notifications
/// are not gated.
/// </summary>
[DomName("Notification")]
public sealed class Notification : EventTarget
{
    private const String CloseEventName = "close";

    private readonly IWindow _window;
    private readonly INotificationPlatform _platform;
    private readonly IEventLoop _eventLoop;
    private readonly TaskCompletionSource<Boolean> _delivery;
    private readonly Object _sync;

    private Phase _phase;
    private Task _tail;

    /// <summary>
    /// Creates a new notification. As in browsers, construction succeeds
    /// without the permission, but the notification is not delivered and
    /// dispatches the error event instead. Events are dispatched in tasks of
    /// the context's event loop; without one, they are dispatched on a
    /// background thread, where <see cref="Delivery"/> tells the outcome to
    /// listeners coming too late.
    /// </summary>
    /// <param name="window">The window creating the notification.</param>
    /// <param name="title">The title of the notification.</param>
    /// <param name="options">The optional options of the notification.</param>
    [DomConstructor]
    public Notification(IWindow window, String title, NotificationOptions options = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _platform = window.Document?.Context?.GetService<INotificationPlatform>() ?? throw new DomException(DomError.NotSupported);

        // the spec throws a TypeError for this combination
        if (options != null && options.Renotify && String.IsNullOrEmpty(options.Tag))
        {
            throw new ArgumentException("Renotifying requires a tag.", nameof(options));
        }

        _eventLoop = window.Document.Context.GetService<IEventLoop>();
        _delivery = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
        _sync = new Object();
        _tail = Task.CompletedTask;
        _phase = Phase.Pending;

        Title = title ?? String.Empty;
        Direction = options?.Direction ?? NotificationDirection.Auto;
        Language = options?.Language ?? String.Empty;
        Body = options?.Body ?? String.Empty;
        Tag = options?.Tag ?? String.Empty;
        Image = ResolveUrl(options?.Image);
        Icon = ResolveUrl(options?.Icon);
        Badge = ResolveUrl(options?.Badge);
        Timestamp = options?.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Renotify = options?.Renotify ?? false;
        Silent = options?.Silent;
        RequireInteraction = options?.RequireInteraction ?? false;
        Data = options?.Data;
        Origin = LockManager.GetOrigin(window);

        _window.Unloaded += OnUnload;
        Post(Start);
    }

    /// <summary>
    /// Gets the current permission for the origin of the given window.
    /// </summary>
    /// <param name="window">The window asking for the permission.</param>
    /// <returns>The permission, which is granted if there is no policy.</returns>
    [DomName("permission")]
    [DomAccessor(Accessors.Getter)]
    public static NotificationPermission GetPermission(IWindow window)
    {
        var permissions = Permissions.ForWindow(window);
        return permissions is null ? NotificationPermission.Granted : Convert(permissions.GetState(PermissionNames.Notifications));
    }

    /// <summary>
    /// Requests the permission for the origin of the given window, which is
    /// answered by the registered policy.
    /// </summary>
    /// <param name="window">The window requesting the permission.</param>
    /// <returns>The permission, which stays default for a dismissed prompt.</returns>
    [DomName("requestPermission")]
    public static async Task<NotificationPermission> RequestPermission(IWindow window)
    {
        var permissions = Permissions.ForWindow(window);

        if (permissions is null)
        {
            return NotificationPermission.Granted;
        }

        var state = await permissions.RequestAsync(PermissionNames.Notifications).ConfigureAwait(false);

        // the request turns a dismissed prompt into denied, while the spec
        // resolves with the state that is still in place
        return state == PermissionState.Granted ? NotificationPermission.Granted : Convert(permissions.GetState(PermissionNames.Notifications));
    }

    /// <summary>
    /// Gets the title of the notification.
    /// </summary>
    [DomName("title")]
    public String Title { get; }

    /// <summary>
    /// Gets the text direction of the notification.
    /// </summary>
    [DomName("dir")]
    public NotificationDirection Direction { get; }

    /// <summary>
    /// Gets the language of the notification.
    /// </summary>
    [DomName("lang")]
    public String Language { get; }

    /// <summary>
    /// Gets the body text of the notification.
    /// </summary>
    [DomName("body")]
    public String Body { get; }

    /// <summary>
    /// Gets the tag grouping the notification. A platform may use the tag and
    /// the <see cref="Origin"/> to replace the previous notification.
    /// </summary>
    [DomName("tag")]
    public String Tag { get; }

    /// <summary>
    /// Gets the URL of the image, or empty if there is none.
    /// </summary>
    [DomName("image")]
    public String Image { get; }

    /// <summary>
    /// Gets the URL of the icon, or empty if there is none.
    /// </summary>
    [DomName("icon")]
    public String Icon { get; }

    /// <summary>
    /// Gets the URL of the badge, or empty if there is none.
    /// </summary>
    [DomName("badge")]
    public String Badge { get; }

    /// <summary>
    /// Gets the time of the notification in milliseconds since the epoch.
    /// </summary>
    [DomName("timestamp")]
    public Double Timestamp { get; }

    /// <summary>
    /// Gets if replacing a notification with the same tag alerts the user.
    /// </summary>
    [DomName("renotify")]
    public Boolean Renotify { get; }

    /// <summary>
    /// Gets if the notification is silent, or null for the platform's default.
    /// </summary>
    [DomName("silent")]
    public Boolean? Silent { get; }

    /// <summary>
    /// Gets if the notification stays until the user interacted with it.
    /// </summary>
    [DomName("requireInteraction")]
    public Boolean RequireInteraction { get; }

    /// <summary>
    /// Gets the data associated with the notification.
    /// </summary>
    [DomName("data")]
    public Object Data { get; }

    /// <summary>
    /// Gets the origin that created the notification.
    /// </summary>
    public String Origin { get; }

    /// <summary>
    /// Gets the task telling if the notification has been shown. It completes
    /// with false if the permission was missing, the platform failed, or the
    /// notification was closed before being passed to the platform.
    /// </summary>
    public Task<Boolean> Delivery => _delivery.Task;

    /// <summary>
    /// Adds or removes the handler for the click event.
    /// </summary>
    [DomName("onclick")]
    public event DomEventHandler Click
    {
        add { AddEventListener(EventNames.Click, value, false); }
        remove { RemoveEventListener(EventNames.Click, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the show event.
    /// </summary>
    [DomName("onshow")]
    public event DomEventHandler Show
    {
        add { AddEventListener(EventNames.Show, value, false); }
        remove { RemoveEventListener(EventNames.Show, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the error event.
    /// </summary>
    [DomName("onerror")]
    public event DomEventHandler Error
    {
        add { AddEventListener(EventNames.Error, value, false); }
        remove { RemoveEventListener(EventNames.Error, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the close event.
    /// </summary>
    [DomName("onclose")]
    public event DomEventHandler Closed
    {
        add { AddEventListener(CloseEventName, value, false); }
        remove { RemoveEventListener(CloseEventName, value, false); }
    }

    /// <summary>
    /// Closes the notification.
    /// </summary>
    [DomName("close")]
    public void Close()
    {
        var wasShown = false;

        lock (_sync)
        {
            switch (_phase)
            {
                case Phase.Pending:
                    _phase = Phase.Closed;
                    break;
                case Phase.Showing:
                    // the platform has it already, so closing has to wait
                    // for it to be shown; see ShowAsync
                    _phase = Phase.Closing;
                    return;
                case Phase.Shown:
                    _phase = Phase.Closed;
                    wasShown = true;
                    break;
                default:
                    return;
            }
        }

        Release();

        if (wasShown)
        {
            Remove();
        }
        else
        {
            _delivery.TrySetResult(false);
        }
    }

    /// <summary>
    /// Tells that the user clicked the notification, which dispatches the
    /// click event. To be called by the platform.
    /// </summary>
    public void NotifyClicked()
    {
        lock (_sync)
        {
            if (_phase != Phase.Shown)
            {
                return;
            }
        }

        Fire(EventNames.Click);
    }

    /// <summary>
    /// Tells that the user dismissed the notification, which dispatches the
    /// close event. To be called by the platform.
    /// </summary>
    public void NotifyClosed()
    {
        lock (_sync)
        {
            if (_phase != Phase.Shown)
            {
                return;
            }

            _phase = Phase.Closed;
        }

        Release();
        Fire(CloseEventName);
    }

    private void Start()
    {
        lock (_sync)
        {
            if (_phase != Phase.Pending)
            {
                return;
            }

            _phase = Phase.Showing;
        }

        // never prompts: only requestPermission asks, as in the spec
        if (GetPermission(_window) == NotificationPermission.Granted)
        {
            _ = ShowAsync();
        }
        else
        {
            Fail();
        }
    }

    private async Task ShowAsync()
    {
        try
        {
            await _platform.ShowAsync(this).ConfigureAwait(false);
        }
        catch
        {
            Fail();
            return;
        }

        Boolean isClosing;

        lock (_sync)
        {
            isClosing = _phase == Phase.Closing;
            _phase = isClosing ? Phase.Closed : Phase.Shown;
        }

        _delivery.TrySetResult(true);
        Fire(EventNames.Show);

        if (isClosing)
        {
            Release();
            Remove();
        }
    }

    private void Fail()
    {
        lock (_sync)
        {
            _phase = Phase.Closed;
        }

        Release();
        _delivery.TrySetResult(false);
        Fire(EventNames.Error);
    }

    private void Remove()
    {
        _ = RemoveFromPlatformAsync();
        Fire(CloseEventName);
    }

    private async Task RemoveFromPlatformAsync()
    {
        try
        {
            await _platform.CloseAsync(this).ConfigureAwait(false);
        }
        catch
        {
            // the notification is closed for the script in any case
        }
    }

    private void Release() => _window.Unloaded -= OnUnload;

    private void OnUnload(Object sender, Event ev) => Close();

    private void Fire(String name) => Post(() => Dispatch(new Event(name)));

    private void Post(Action action)
    {
        if (_eventLoop is not null)
        {
            _eventLoop.Enqueue(_ => action.Invoke(), TaskPriority.Normal);
            return;
        }

        // running it right away would dispatch the first events from within
        // the constructor, i.e., before anyone could listen to them
        lock (_sync)
        {
            _tail = _tail.ContinueWith(_ => action.Invoke(), TaskScheduler.Default);
        }
    }

    private String ResolveUrl(String value)
    {
        if (String.IsNullOrEmpty(value))
        {
            return String.Empty;
        }

        // as in the spec, a value that cannot be parsed is ignored
        var url = new Url(new Url(_window.Document.Url), value);
        return url.IsInvalid ? String.Empty : url.Href;
    }

    private static NotificationPermission Convert(PermissionState state) => state switch
    {
        PermissionState.Granted => NotificationPermission.Granted,
        PermissionState.Denied => NotificationPermission.Denied,
        _ => NotificationPermission.Default,
    };

    private enum Phase
    {
        Pending,
        Showing,
        Shown,
        Closing,
        Closed,
    }
}

/// <summary>
/// Represents the options for creating a notification.
/// </summary>
[DomName("NotificationOptions")]
public sealed class NotificationOptions
{
    /// <summary>
    /// Gets or sets the text direction.
    /// </summary>
    [DomName("dir")]
    public NotificationDirection Direction { get; set; } = NotificationDirection.Auto;

    /// <summary>
    /// Gets or sets the language.
    /// </summary>
    [DomName("lang")]
    public String Language { get; set; }

    /// <summary>
    /// Gets or sets the body text.
    /// </summary>
    [DomName("body")]
    public String Body { get; set; }

    /// <summary>
    /// Gets or sets the tag grouping the notification.
    /// </summary>
    [DomName("tag")]
    public String Tag { get; set; }

    /// <summary>
    /// Gets or sets the URL of the image, relative to the current document.
    /// </summary>
    [DomName("image")]
    public String Image { get; set; }

    /// <summary>
    /// Gets or sets the URL of the icon, relative to the current document.
    /// </summary>
    [DomName("icon")]
    public String Icon { get; set; }

    /// <summary>
    /// Gets or sets the URL of the badge, relative to the current document.
    /// </summary>
    [DomName("badge")]
    public String Badge { get; set; }

    /// <summary>
    /// Gets or sets the time in milliseconds since the epoch. By default,
    /// this is the time of creating the notification.
    /// </summary>
    [DomName("timestamp")]
    public Double? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets if replacing a notification with the same tag alerts the
    /// user, which requires a tag.
    /// </summary>
    [DomName("renotify")]
    public Boolean Renotify { get; set; }

    /// <summary>
    /// Gets or sets if the notification is silent, or null for the platform's default.
    /// </summary>
    [DomName("silent")]
    public Boolean? Silent { get; set; }

    /// <summary>
    /// Gets or sets if the notification stays until the user interacted with it.
    /// </summary>
    [DomName("requireInteraction")]
    public Boolean RequireInteraction { get; set; }

    /// <summary>
    /// Gets or sets the data associated with the notification.
    /// </summary>
    [DomName("data")]
    public Object Data { get; set; }
}

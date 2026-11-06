namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom;
using AngleSharp.Io.PermissionsApi;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents the PushManager interface, which holds the subscription of a
/// registration. Talking to a push service is up to the
/// <see cref="IPushPlatform"/>.
/// </summary>
[DomName("PushManager")]
public sealed class PushManager
{
    private const Int32 PublicKeyLength = 65;
    private const Byte UncompressedPointPrefix = 0x04;

    private static readonly String[] ContentEncodings = new[] { "aes128gcm", "aesgcm" };

    private readonly ServiceWorkerRegistration _registration;
    private readonly IPushPlatform _platform;
    private readonly SemaphoreSlim _operations;

    private PushSubscription _subscription;

    internal PushManager(ServiceWorkerRegistration registration, IPushPlatform platform)
    {
        _registration = registration;
        _platform = platform;
        _operations = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Gets the content encodings a push message may use. Decrypting them is
    /// up to the platform.
    /// </summary>
    [DomName("supportedContentEncodings")]
    public static String[] SupportedContentEncodings => (String[])ContentEncodings.Clone();

    /// <summary>
    /// Subscribes to the push service unless there is a subscription with
    /// the same options, which is returned instead.
    /// </summary>
    /// <param name="options">The optional options of the subscription.</param>
    /// <returns>The subscription.</returns>
    [DomName("subscribe")]
    public async Task<PushSubscription> Subscribe(PushSubscriptionOptionsInit options = null)
    {
        var scope = new Url(_registration.Scope);

        if (!ServiceWorkerContainer.IsPotentiallyTrustworthy(scope))
        {
            throw new DomException(Permissions.NotAllowedError);
        }

        var requested = new PushSubscriptionOptions(options?.UserVisibleOnly ?? false, ParseKey(options?.ApplicationServerKey));

        if (_registration.Active is null)
        {
            throw new DomException(DomError.InvalidState);
        }

        await EnsureGrantedAsync(requested.UserVisibleOnly).ConfigureAwait(false);

        // serialized like the spec's parallel queue, otherwise concurrent
        // calls would create subscriptions overwriting each other
        await _operations.WaitAsync().ConfigureAwait(false);

        try
        {
            var current = GetActiveSubscription();

            if (current != null)
            {
                if (!HaveSameValues(current.Options, requested))
                {
                    throw new DomException(DomError.InvalidState);
                }

                return current;
            }

            var data = await CreateSubscriptionAsync(scope, requested).ConfigureAwait(false);
            _subscription = new PushSubscription(this, data, requested);
            return _subscription;
        }
        finally
        {
            _operations.Release();
        }
    }

    /// <summary>
    /// Gets the active subscription.
    /// </summary>
    /// <returns>The subscription or null.</returns>
    [DomName("getSubscription")]
    public async Task<PushSubscription> GetSubscription()
    {
        await _operations.WaitAsync().ConfigureAwait(false);

        try
        {
            return GetActiveSubscription();
        }
        finally
        {
            _operations.Release();
        }
    }

    /// <summary>
    /// Gets the state of the permission for subscribing using the given options.
    /// </summary>
    /// <param name="options">The optional options of the subscription.</param>
    /// <returns>The state, which is granted if there is no policy.</returns>
    [DomName("permissionState")]
    public Task<PermissionState> PermissionState(PushSubscriptionOptionsInit options = null)
    {
        var permissions = _registration.Permissions;
        var state = Dom.PermissionState.Granted;

        if (permissions != null)
        {
            state = permissions.GetState(PermissionNames.Push);

            if (options?.UserVisibleOnly == true)
            {
                state = Combine(state, permissions.GetState(PermissionNames.Notifications));
            }
        }

        return Task.FromResult(state);
    }

    internal Boolean HasEndpoint(String endpoint) =>
        String.Equals(Volatile.Read(ref _subscription)?.Endpoint, endpoint, StringComparison.Ordinal);

    internal Task DispatchAsync(ServiceWorker worker, PushMessageData data) => _platform.DispatchAsync(worker, data);

    internal async Task<Boolean> UnsubscribeAsync(PushSubscription subscription)
    {
        await _operations.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!ReferenceEquals(GetActiveSubscription(), subscription))
            {
                return false;
            }

            _subscription = null;
            await RemoveFromPlatformAsync(subscription).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _operations.Release();
        }
    }

    internal async Task TerminateAsync()
    {
        var subscription = await GetSubscription().ConfigureAwait(false);

        if (subscription != null)
        {
            await UnsubscribeAsync(subscription).ConfigureAwait(false);
        }
    }

    private async Task EnsureGrantedAsync(Boolean userVisibleOnly)
    {
        var permissions = _registration.Permissions;

        if (permissions != null)
        {
            await permissions.EnsureGrantedAsync(PermissionNames.Push).ConfigureAwait(false);

            // messages of such a subscription end in notifications, so a
            // subscription would be pointless without being allowed to show them
            if (userVisibleOnly)
            {
                await permissions.EnsureGrantedAsync(PermissionNames.Notifications).ConfigureAwait(false);
            }
        }
    }

    private async Task<PushSubscriptionData> CreateSubscriptionAsync(Url scope, PushSubscriptionOptions options)
    {
        PushSubscriptionData data;

        try
        {
            data = await _platform.SubscribeAsync(scope.Origin, _registration.Scope, options).ConfigureAwait(false);
        }
        catch (DomException)
        {
            throw;
        }
        catch
        {
            throw new DomException(DomError.Abort);
        }

        if (String.IsNullOrEmpty(data?.Endpoint))
        {
            throw new DomException(DomError.Abort);
        }

        return data;
    }

    private async Task RemoveFromPlatformAsync(PushSubscription subscription)
    {
        try
        {
            await _platform.UnsubscribeAsync(subscription).ConfigureAwait(false);
        }
        catch
        {
            // the spec deactivates the subscription in any case and only
            // leaves the request to the push service (and its retries) open
        }
    }

    private PushSubscription GetActiveSubscription()
    {
        var subscription = _subscription;
        var now = (Double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (subscription?.ExpirationTime <= now)
        {
            // the push service dropped it on its own, no need to tell it
            _subscription = null;
            return null;
        }

        return subscription;
    }

    private static Byte[] ParseKey(Object key)
    {
        Byte[] bytes;

        switch (key)
        {
            case null:
                return null;
            case Byte[] array:
                bytes = (Byte[])array.Clone();
                break;
            case ArraySegment<Byte> segment when segment.Array != null:
                bytes = segment.ToArray();
                break;
            case String text:
                if (!Base64Url.TryDecode(text, out bytes))
                {
                    throw new DomException(DomError.InvalidCharacter);
                }

                break;
            default:
                throw new DomException(DomError.InvalidAccess);
        }

        // only the form of the point is checked: checking if it lies on the
        // curve needs ECDH, which is not available on all target frameworks
        if (bytes.Length != PublicKeyLength || bytes[0] != UncompressedPointPrefix)
        {
            throw new DomException(DomError.InvalidAccess);
        }

        return bytes;
    }

    private static Boolean HaveSameValues(PushSubscriptionOptions a, PushSubscriptionOptions b)
    {
        var keyA = a.ApplicationServerKey ?? Array.Empty<Byte>();
        var keyB = b.ApplicationServerKey ?? Array.Empty<Byte>();
        return a.UserVisibleOnly == b.UserVisibleOnly && keyA.SequenceEqual(keyB);
    }

    private static PermissionState Combine(PermissionState a, PermissionState b)
    {
        if (a == Dom.PermissionState.Denied || b == Dom.PermissionState.Denied)
        {
            return Dom.PermissionState.Denied;
        }

        return a == Dom.PermissionState.Granted && b == Dom.PermissionState.Granted ? Dom.PermissionState.Granted : Dom.PermissionState.Prompt;
    }
}

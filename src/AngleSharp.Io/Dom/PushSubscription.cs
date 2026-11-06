namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Represents the subscription of a registration at a push service.
/// </summary>
[DomName("PushSubscription")]
public sealed class PushSubscription
{
    private readonly PushManager _manager;
    private readonly Byte[] _p256dh;
    private readonly Byte[] _auth;

    internal PushSubscription(PushManager manager, PushSubscriptionData data, PushSubscriptionOptions options)
    {
        _manager = manager;
        _p256dh = data.P256dh;
        _auth = data.Auth;
        Endpoint = data.Endpoint;
        ExpirationTime = data.ExpirationTime;
        Options = options;
    }

    /// <summary>
    /// Gets the URL for sending messages to the subscription.
    /// </summary>
    [DomName("endpoint")]
    public String Endpoint { get; }

    /// <summary>
    /// Gets the time of expiration in milliseconds since the epoch, or null
    /// if the subscription does not expire.
    /// </summary>
    [DomName("expirationTime")]
    public Double? ExpirationTime { get; }

    /// <summary>
    /// Gets the options of the subscription.
    /// </summary>
    [DomName("options")]
    public PushSubscriptionOptions Options { get; }

    /// <summary>
    /// Gets the requested key of the subscription.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <returns>A copy of the key, or null if there is no such key.</returns>
    [DomName("getKey")]
    public Byte[] GetKey(PushEncryptionKeyName name) => name switch
    {
        PushEncryptionKeyName.P256dh => (Byte[])_p256dh?.Clone(),
        PushEncryptionKeyName.Auth => (Byte[])_auth?.Clone(),
        _ => null,
    };

    /// <summary>
    /// Removes the subscription.
    /// </summary>
    /// <returns>True if the subscription was active, otherwise false.</returns>
    [DomName("unsubscribe")]
    public Task<Boolean> Unsubscribe() => _manager.UnsubscribeAsync(this);

    /// <summary>
    /// Gets the members to serialize for sending the subscription to a server.
    /// </summary>
    /// <returns>The endpoint, the expiration time, and the base64url encoded keys.</returns>
    [DomName("toJSON")]
    public IDictionary<String, Object> ToJson()
    {
        var keys = new Dictionary<String, Object>(StringComparer.Ordinal);

        if (_p256dh != null)
        {
            keys["p256dh"] = Base64Url.Encode(_p256dh);
        }

        if (_auth != null)
        {
            keys["auth"] = Base64Url.Encode(_auth);
        }

        return new Dictionary<String, Object>(StringComparer.Ordinal)
        {
            ["endpoint"] = Endpoint,
            ["expirationTime"] = ExpirationTime,
            ["keys"] = keys,
        };
    }
}

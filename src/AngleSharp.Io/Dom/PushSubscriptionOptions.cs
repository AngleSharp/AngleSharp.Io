namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;

/// <summary>
/// Represents the options passed for creating a push subscription.
/// </summary>
[DomName("PushSubscriptionOptionsInit")]
public sealed class PushSubscriptionOptionsInit
{
    /// <summary>
    /// Gets or sets if every message results in a visible notification, which
    /// makes the subscription also require the permission for notifications.
    /// </summary>
    [DomName("userVisibleOnly")]
    public Boolean UserVisibleOnly { get; set; }

    /// <summary>
    /// Gets or sets the public key (P-256 ECDH) of the application server,
    /// either as bytes or as a base64url encoded string.
    /// </summary>
    [DomName("applicationServerKey")]
    public Object ApplicationServerKey { get; set; }
}

/// <summary>
/// Represents the options of a push subscription.
/// </summary>
[DomName("PushSubscriptionOptions")]
public sealed class PushSubscriptionOptions
{
    /// <summary>
    /// Creates new options.
    /// </summary>
    /// <param name="userVisibleOnly">If every message results in a visible notification.</param>
    /// <param name="applicationServerKey">The public key of the application server, if any.</param>
    public PushSubscriptionOptions(Boolean userVisibleOnly, Byte[] applicationServerKey)
    {
        UserVisibleOnly = userVisibleOnly;
        ApplicationServerKey = applicationServerKey;
    }

    /// <summary>
    /// Gets if every message results in a visible notification.
    /// </summary>
    [DomName("userVisibleOnly")]
    public Boolean UserVisibleOnly { get; }

    /// <summary>
    /// Gets the public key of the application server, if any.
    /// </summary>
    [DomName("applicationServerKey")]
    public Byte[] ApplicationServerKey { get; }
}

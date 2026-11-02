namespace AngleSharp.Io.Dom;

using System;
using System.Threading.Tasks;

/// <summary>
/// Represents the transport to a push service and the delivery of its
/// messages to the service workers. Decrypting the messages is up to the
/// platform.
/// </summary>
public interface IPushPlatform
{
    /// <summary>
    /// Creates a subscription at the push service. A faulted task or a
    /// missing result rejects with an AbortError, unless it is faulted with a
    /// DomException such as a NotSupportedError, which is passed on.
    /// </summary>
    /// <param name="origin">The origin of the subscribing registration.</param>
    /// <param name="scope">The scope of the subscribing registration.</param>
    /// <param name="options">The validated options of the subscription.</param>
    /// <returns>The details of the created subscription.</returns>
    Task<PushSubscriptionData> SubscribeAsync(String origin, String scope, PushSubscriptionOptions options);

    /// <summary>
    /// Removes a subscription at the push service. As in the spec, the
    /// subscription is already deactivated at this point and a faulted task
    /// does not change that, so retrying is up to the platform.
    /// </summary>
    /// <param name="subscription">The removed subscription.</param>
    /// <returns>The task representing the operation.</returns>
    Task UnsubscribeAsync(PushSubscription subscription);

    /// <summary>
    /// Runs the push event of the given worker, see
    /// <see cref="ServiceWorkerApi.ServiceWorkerRegistry.DeliverPushAsync"/>.
    /// </summary>
    /// <param name="worker">The active worker of the subscribed registration.</param>
    /// <param name="data">The decrypted payload, or null if the message has none.</param>
    /// <returns>The task representing the event.</returns>
    Task DispatchAsync(ServiceWorker worker, PushMessageData data);
}

/// <summary>
/// Represents the details of a subscription created by a push service.
/// </summary>
public sealed class PushSubscriptionData
{
    /// <summary>
    /// Gets or sets the URL for sending messages to the subscription.
    /// </summary>
    public String Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the time of expiration in milliseconds since the epoch,
    /// or null if the subscription does not expire.
    /// </summary>
    public Double? ExpirationTime { get; set; }

    /// <summary>
    /// Gets or sets the public key (P-256 ECDH) of the subscription.
    /// </summary>
    public Byte[] P256dh { get; set; }

    /// <summary>
    /// Gets or sets the authentication secret of the subscription.
    /// </summary>
    public Byte[] Auth { get; set; }
}

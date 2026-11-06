namespace AngleSharp.Io.PushApi;

using AngleSharp.Io.Dom;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

/// <summary>
/// Represents a push platform delegating to optional callbacks. Without
/// callbacks, no push service is involved: subscriptions get a generated
/// endpoint below https://push.invalid/ with random keys, and messages are
/// dropped.
/// </summary>
public sealed class PushPlatform : IPushPlatform
{
    private const Int32 PublicKeyLength = 65;
    private const Int32 AuthLength = 16;

    /// <summary>
    /// Gets or sets the callback creating a subscription for the given
    /// origin and scope.
    /// </summary>
    public Func<String, String, PushSubscriptionOptions, Task<PushSubscriptionData>> Subscribe { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked for a removed subscription.
    /// </summary>
    public Func<PushSubscription, Task> Unsubscribe { get; set; }

    /// <summary>
    /// Gets or sets the callback running the push event of the given worker.
    /// </summary>
    public Func<ServiceWorker, PushMessageData, Task> Dispatch { get; set; }

    /// <inheritdoc />
    public Task<PushSubscriptionData> SubscribeAsync(String origin, String scope, PushSubscriptionOptions options) =>
        Subscribe?.Invoke(origin, scope, options) ?? Task.FromResult(CreateSubscription());

    /// <inheritdoc />
    public Task UnsubscribeAsync(PushSubscription subscription) => Unsubscribe?.Invoke(subscription) ?? Task.CompletedTask;

    /// <inheritdoc />
    public Task DispatchAsync(ServiceWorker worker, PushMessageData data) => Dispatch?.Invoke(worker, data) ?? Task.CompletedTask;

    private static PushSubscriptionData CreateSubscription()
    {
        var publicKey = CreateRandomBytes(PublicKeyLength);

        // the shape of an uncompressed point; nothing can be encrypted for
        // it, which is fine as no push service is sending anything
        publicKey[0] = 0x04;

        return new PushSubscriptionData
        {
            Endpoint = "https://push.invalid/" + Guid.NewGuid().ToString("N"),
            P256dh = publicKey,
            Auth = CreateRandomBytes(AuthLength),
        };
    }

    private static Byte[] CreateRandomBytes(Int32 length)
    {
        var bytes = new Byte[length];

        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        return bytes;
    }
}

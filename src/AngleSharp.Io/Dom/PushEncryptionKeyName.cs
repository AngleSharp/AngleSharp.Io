namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;

/// <summary>
/// The names of the keys of a push subscription.
/// </summary>
[DomLiterals]
[DomName("PushEncryptionKeyName")]
public enum PushEncryptionKeyName
{
    /// <summary>
    /// The public key (P-256 ECDH).
    /// </summary>
    [DomName("p256dh")]
    P256dh,
    /// <summary>
    /// The authentication secret.
    /// </summary>
    [DomName("auth")]
    Auth,
}

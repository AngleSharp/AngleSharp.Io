namespace AngleSharp.Io;

using System;

/// <summary>
/// Options for sending beacons via navigator.sendBeacon.
/// </summary>
public sealed class BeaconOptions
{
    /// <summary>
    /// The quota used by browsers, i.e., 64 KiB.
    /// </summary>
    public const Int64 DefaultQuota = 64 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of body bytes that may be pending,
    /// i.e., queued or in flight, per browsing context. A beacon exceeding
    /// the quota is rejected.
    /// </summary>
    public Int64 Quota { get; set; } = DefaultQuota;

    /// <summary>
    /// Gets or sets the maximum number of beacons sent at the same time per
    /// browsing context. Further beacons are queued and sent in order once
    /// a running one completes. By default, this is 0, which means that all
    /// beacons are sent right away.
    /// </summary>
    public Int32 MaxConcurrentRequests { get; set; }
}

namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom.Events;
using System;

/// <summary>
/// Represents the event telling the orientation of the device. A host with
/// sensors fires it at the window (deviceorientation). For more information see:
/// https://w3c.github.io/deviceorientation/#deviceorientation
/// </summary>
[DomName("DeviceOrientationEvent")]
[DomExposed("Window")]
public class DeviceOrientationEvent : Event
{
    /// <summary>
    /// Creates a new device orientation event.
    /// </summary>
    /// <param name="type">The type of the event.</param>
    /// <param name="bubbles">If the event is bubbling.</param>
    /// <param name="cancelable">If the event is cancelable.</param>
    /// <param name="alpha">The rotation around the z axis in degrees.</param>
    /// <param name="beta">The rotation around the x axis in degrees.</param>
    /// <param name="gamma">The rotation around the y axis in degrees.</param>
    /// <param name="absolute">If the orientation is relative to the earth.</param>
    [DomConstructor]
    [DomInitDict(offset: 1, optional: true)]
    public DeviceOrientationEvent(String type, Boolean bubbles = false, Boolean cancelable = false, Double? alpha = null, Double? beta = null, Double? gamma = null, Boolean absolute = false)
        : base(type, bubbles, cancelable)
    {
        Alpha = alpha;
        Beta = beta;
        Gamma = gamma;
        IsAbsolute = absolute;
    }

    /// <summary>
    /// Gets the rotation around the z axis in degrees, if known.
    /// </summary>
    [DomName("alpha")]
    public Double? Alpha { get; }

    /// <summary>
    /// Gets the rotation around the x axis in degrees, if known.
    /// </summary>
    [DomName("beta")]
    public Double? Beta { get; }

    /// <summary>
    /// Gets the rotation around the y axis in degrees, if known.
    /// </summary>
    [DomName("gamma")]
    public Double? Gamma { get; }

    /// <summary>
    /// Gets if the orientation is relative to the earth, not to an arbitrary frame.
    /// </summary>
    [DomName("absolute")]
    public Boolean IsAbsolute { get; }
}

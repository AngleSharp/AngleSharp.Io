namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom.Events;
using System;

/// <summary>
/// Represents the event telling the acceleration and rotation of the device. A
/// host with sensors fires it at the window (devicemotion). For more information see:
/// https://w3c.github.io/deviceorientation/#devicemotion
/// </summary>
[DomName("DeviceMotionEvent")]
[DomExposed("Window")]
public class DeviceMotionEvent : Event
{
    /// <summary>
    /// Creates a new device motion event.
    /// </summary>
    /// <param name="type">The type of the event.</param>
    /// <param name="bubbles">If the event is bubbling.</param>
    /// <param name="cancelable">If the event is cancelable.</param>
    /// <param name="acceleration">The acceleration without gravity.</param>
    /// <param name="accelerationIncludingGravity">The acceleration including gravity.</param>
    /// <param name="rotationRate">The rate of rotation.</param>
    /// <param name="interval">The interval of the readings in milliseconds.</param>
    [DomConstructor]
    [DomInitDict(offset: 1, optional: true)]
    public DeviceMotionEvent(String type, Boolean bubbles = false, Boolean cancelable = false, DeviceMotionEventAcceleration acceleration = null, DeviceMotionEventAcceleration accelerationIncludingGravity = null, DeviceMotionEventRotationRate rotationRate = null, Double interval = 0)
        : base(type, bubbles, cancelable)
    {
        Acceleration = acceleration;
        AccelerationIncludingGravity = accelerationIncludingGravity;
        RotationRate = rotationRate;
        Interval = interval;
    }

    /// <summary>
    /// Gets the acceleration without gravity in m/s², if known.
    /// </summary>
    [DomName("acceleration")]
    public DeviceMotionEventAcceleration Acceleration { get; }

    /// <summary>
    /// Gets the acceleration including gravity in m/s², if known.
    /// </summary>
    [DomName("accelerationIncludingGravity")]
    public DeviceMotionEventAcceleration AccelerationIncludingGravity { get; }

    /// <summary>
    /// Gets the rate of rotation in degrees per second, if known.
    /// </summary>
    [DomName("rotationRate")]
    public DeviceMotionEventRotationRate RotationRate { get; }

    /// <summary>
    /// Gets the interval of the readings in milliseconds.
    /// </summary>
    [DomName("interval")]
    public Double Interval { get; }
}

/// <summary>
/// Represents the acceleration of a device along its axes.
/// </summary>
[DomName("DeviceMotionEventAcceleration")]
public sealed class DeviceMotionEventAcceleration
{
    /// <summary>
    /// Gets or sets the acceleration along the x axis.
    /// </summary>
    [DomName("x")]
    public Double? X { get; set; }

    /// <summary>
    /// Gets or sets the acceleration along the y axis.
    /// </summary>
    [DomName("y")]
    public Double? Y { get; set; }

    /// <summary>
    /// Gets or sets the acceleration along the z axis.
    /// </summary>
    [DomName("z")]
    public Double? Z { get; set; }
}

/// <summary>
/// Represents the rate of rotation of a device around its axes.
/// </summary>
[DomName("DeviceMotionEventRotationRate")]
public sealed class DeviceMotionEventRotationRate
{
    /// <summary>
    /// Gets or sets the rotation around the z axis.
    /// </summary>
    [DomName("alpha")]
    public Double? Alpha { get; set; }

    /// <summary>
    /// Gets or sets the rotation around the x axis.
    /// </summary>
    [DomName("beta")]
    public Double? Beta { get; set; }

    /// <summary>
    /// Gets or sets the rotation around the y axis.
    /// </summary>
    [DomName("gamma")]
    public Double? Gamma { get; set; }
}

namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;

/// <summary>
/// Describes the permission to query.
/// </summary>
[DomName("PermissionDescriptor")]
public sealed class PermissionDescriptor
{
    /// <summary>
    /// Gets or sets the name of the permission, e.g., "geolocation".
    /// </summary>
    [DomName("name")]
    public String Name { get; set; }
}

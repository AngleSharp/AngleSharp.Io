namespace AngleSharp.Io;

using System;

/// <summary>
/// Options for setting up the File System Access API.
/// </summary>
public sealed class FileSystemAccessOptions
{
    /// <summary>
    /// Gets or sets if the pickers are limited to secure contexts, i.e., to
    /// https documents and http documents served from a loopback address. By
    /// default, they are. Turn it off, e.g., for documents without an address.
    /// </summary>
    public Boolean RequireSecureContext { get; set; } = true;
}

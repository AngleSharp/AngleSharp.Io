namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using System;

/// <summary>
/// Represents a named <see cref="Blob"/>, as scripts create it. For more
/// information see: https://w3c.github.io/FileAPI/#file-section
/// </summary>
[DomName("File")]
[DomExposed("Window")]
[DomExposed("Worker")]
public class WebFile : Blob, IFile
{
    /// <summary>
    /// Creates a new file from the given parts.
    /// </summary>
    /// <param name="fileBits">The parts: strings, blobs, and buffers.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="options">The type and the last modified date.</param>
    [DomConstructor]
    public WebFile(Object[] fileBits, String fileName, FilePropertyBag options = null)
        : base(fileBits, options)
    {
        Name = fileName ?? String.Empty;
        LastModified = options?.LastModified is Int64 modified
            ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(modified)
            : DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new file over the given bytes, which are not copied.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    /// <param name="name">The name of the file.</param>
    /// <param name="type">The mime-type of the content, if any.</param>
    /// <param name="lastModified">The last modified date, by default now.</param>
    public WebFile(Byte[] content, String name, String type = "", DateTime? lastModified = null)
        : base(content, type)
    {
        Name = name ?? String.Empty;
        LastModified = lastModified ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the name of the file.
    /// </summary>
    public String Name { get; }

    /// <summary>
    /// Gets the last modified date of the file.
    /// </summary>
    public DateTime LastModified { get; }
}

/// <summary>
/// Represents the options for creating a <see cref="WebFile"/>.
/// </summary>
public class FilePropertyBag : BlobPropertyBag
{
    /// <summary>
    /// Gets or sets the last modified date in milliseconds since the epoch.
    /// </summary>
    [DomName("lastModified")]
    public Int64? LastModified { get; set; }
}

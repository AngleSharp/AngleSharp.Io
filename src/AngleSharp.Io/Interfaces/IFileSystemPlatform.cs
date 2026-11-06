namespace AngleSharp.Io.Dom;

using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Represents the platform behind the File System Access API, i.e., the
/// pickers and all access to the files, e.g., the real file system of the
/// host or a test double.
/// </summary>
public interface IFileSystemPlatform
{
    /// <summary>
    /// Lets the user pick existing files.
    /// </summary>
    /// <param name="origin">The origin asking for the files, or empty for an opaque origin.</param>
    /// <param name="options">The options of the picker.</param>
    /// <returns>The picked files; none or null if the picker has been dismissed.</returns>
    Task<FileSystemEntry[]> PickFilesAsync(String origin, OpenFilePickerOptions options);

    /// <summary>
    /// Lets the user pick the file to save to.
    /// </summary>
    /// <param name="origin">The origin asking for the file, or empty for an opaque origin.</param>
    /// <param name="options">The options of the picker.</param>
    /// <returns>The picked file; null if the picker has been dismissed.</returns>
    Task<FileSystemEntry> PickSaveFileAsync(String origin, SaveFilePickerOptions options);

    /// <summary>
    /// Reads the current state of the given file.
    /// </summary>
    /// <param name="entry">The file to read.</param>
    /// <returns>The file, or null if it does not exist (anymore).</returns>
    Task<IFile> ReadFileAsync(FileSystemEntry entry);

    /// <summary>
    /// Replaces the content of the given file. This has to be atomic: a
    /// faulted task must leave the previous content in place.
    /// </summary>
    /// <param name="entry">The file to write.</param>
    /// <param name="content">The new content, only valid until the task completes.</param>
    /// <returns>The task representing the write.</returns>
    Task WriteFileAsync(FileSystemEntry entry, Stream content);
}

/// <summary>
/// Identifies a file of a <see cref="IFileSystemPlatform"/>.
/// </summary>
public sealed class FileSystemEntry
{
    /// <summary>
    /// Creates a new entry.
    /// </summary>
    /// <param name="id">
    /// The identity of the file, which has to stay the same for every entry
    /// of the file, e.g., its full path.
    /// </param>
    /// <param name="name">The name of the file exposed to the document.</param>
    public FileSystemEntry(String id, String name)
    {
        Id = !String.IsNullOrEmpty(id) ? id : throw new ArgumentException("An identity is required.", nameof(id));
        Name = name ?? String.Empty;
    }

    /// <summary>
    /// Gets the identity of the file.
    /// </summary>
    public String Id { get; }

    /// <summary>
    /// Gets the name of the file.
    /// </summary>
    public String Name { get; }
}

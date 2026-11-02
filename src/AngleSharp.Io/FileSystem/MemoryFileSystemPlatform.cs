namespace AngleSharp.Io.FileSystemApi;

using AngleSharp.Io.Dom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Represents a file system platform keeping its files in memory, e.g., for
/// deterministic tests. Files are identified by their name.
/// </summary>
public sealed class MemoryFileSystemPlatform : IFileSystemPlatform
{
    private readonly Dictionary<String, MemoryFile> _files;

    /// <summary>
    /// Creates a new memory file system platform without files.
    /// </summary>
    public MemoryFileSystemPlatform()
    {
        _files = new Dictionary<String, MemoryFile>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets or sets the callback standing in for the user of the open file
    /// picker, returning the names of the picked files; none or null dismiss
    /// the picker. By default, the first file is picked, or all of them if
    /// the options allow it.
    /// </summary>
    public Func<OpenFilePickerOptions, String[]> OpenFilePicker { get; set; }

    /// <summary>
    /// Gets or sets the callback standing in for the user of the save file
    /// picker, returning the name of the file; null dismisses the picker. As
    /// in browsers, a new file exists right away. By default, the suggested
    /// name is picked.
    /// </summary>
    public Func<SaveFilePickerOptions, String> SaveFilePicker { get; set; }

    /// <summary>
    /// Gets the names of the files.
    /// </summary>
    public String[] FileNames
    {
        get
        {
            lock (_files)
            {
                return _files.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }
        }
    }

    /// <summary>
    /// Creates or replaces the file with the given name.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="content">The content of the file.</param>
    /// <param name="type">The MIME type of the file.</param>
    public void SetFile(String name, Byte[] content, String type = "")
    {
        if (String.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A name is required.", nameof(name));
        }

        var copy = (Byte[])(content ?? throw new ArgumentNullException(nameof(content))).Clone();

        lock (_files)
        {
            _files[name] = new MemoryFile(copy, type ?? String.Empty, DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Gets the content of the file with the given name.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>A copy of the content, or null if there is no such file.</returns>
    public Byte[] GetFile(String name)
    {
        lock (_files)
        {
            return name != null && _files.TryGetValue(name, out var file) ? (Byte[])file.Content.Clone() : null;
        }
    }

    /// <inheritdoc />
    public Task<FileSystemEntry[]> PickFilesAsync(String origin, OpenFilePickerOptions options)
    {
        var names = OpenFilePicker is null ? GetDefaultPick(options) : OpenFilePicker.Invoke(options);
        var entries = (names ?? Array.Empty<String>()).Select(name => new FileSystemEntry(name, name)).ToArray();
        return Task.FromResult(entries);
    }

    /// <inheritdoc />
    public Task<FileSystemEntry> PickSaveFileAsync(String origin, SaveFilePickerOptions options)
    {
        var name = SaveFilePicker is null ? options?.SuggestedName ?? "untitled" : SaveFilePicker.Invoke(options);

        if (name is null)
        {
            return Task.FromResult<FileSystemEntry>(null);
        }

        lock (_files)
        {
            if (!_files.ContainsKey(name))
            {
                _files[name] = new MemoryFile(Array.Empty<Byte>(), String.Empty, DateTime.UtcNow);
            }
        }

        return Task.FromResult(new FileSystemEntry(name, name));
    }

    /// <inheritdoc />
    public Task<IFile> ReadFileAsync(FileSystemEntry entry)
    {
        lock (_files)
        {
            if (!_files.TryGetValue(entry.Id, out var file))
            {
                return Task.FromResult<IFile>(null);
            }

            // every read is a snapshot, as a File does not follow later writes
            return Task.FromResult<IFile>(new InputFile(entry.Name, file.Type, new MemoryStream((Byte[])file.Content.Clone()), file.Modified));
        }
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(FileSystemEntry entry, Stream content)
    {
        using var target = new MemoryStream();
        await content.CopyToAsync(target).ConfigureAwait(false);

        lock (_files)
        {
            var type = _files.TryGetValue(entry.Id, out var file) ? file.Type : String.Empty;
            _files[entry.Id] = new MemoryFile(target.ToArray(), type, DateTime.UtcNow);
        }
    }

    private String[] GetDefaultPick(OpenFilePickerOptions options)
    {
        var names = FileNames;
        return options?.Multiple == true ? names : names.Take(1).ToArray();
    }

    private sealed class MemoryFile
    {
        public MemoryFile(Byte[] content, String type, DateTime modified)
        {
            Content = content;
            Type = type;
            Modified = modified;
        }

        public Byte[] Content { get; }

        public String Type { get; }

        public DateTime Modified { get; }
    }
}

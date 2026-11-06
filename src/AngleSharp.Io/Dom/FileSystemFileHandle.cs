namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Represents a handle to a file.
/// </summary>
[DomName("FileSystemFileHandle")]
public sealed class FileSystemFileHandle : FileSystemHandle
{
    internal FileSystemFileHandle(IFileSystemPlatform platform, FileSystemEntry entry, Permissions permissions)
        : base(platform, entry, permissions)
    {
    }

    /// <inheritdoc />
    public override FileSystemHandleKind Kind => FileSystemHandleKind.File;

    /// <summary>
    /// Gets the current state of the file.
    /// </summary>
    /// <returns>The file.</returns>
    [DomName("getFile")]
    public async Task<IFile> GetFile()
    {
        await EnsureAccessAsync(FileSystemPermissionMode.Read).ConfigureAwait(false);
        var file = await Platform.ReadFileAsync(Entry).ConfigureAwait(false);
        return file ?? throw new DomException(DomError.NotFound);
    }

    /// <summary>
    /// Creates a writable stream. Its changes end up in the file only when
    /// the stream is closed.
    /// </summary>
    /// <param name="options">The optional options of the stream.</param>
    /// <returns>The stream.</returns>
    [DomName("createWritable")]
    public async Task<FileSystemWritableFileStream> CreateWritable(FileSystemCreateWritableOptions options = null)
    {
        await EnsureAccessAsync(FileSystemPermissionMode.ReadWrite).ConfigureAwait(false);
        var buffer = new MemoryStream();

        if (options?.KeepExistingData == true)
        {
            // a file picked for saving may not exist until the first commit
            using var file = await Platform.ReadFileAsync(Entry).ConfigureAwait(false);

            if (file?.Body is Stream body)
            {
                if (body.CanSeek)
                {
                    body.Seek(0, SeekOrigin.Begin);
                }

                await body.CopyToAsync(buffer).ConfigureAwait(false);
                buffer.Position = 0;
            }
        }

        return new FileSystemWritableFileStream(this, buffer);
    }
}

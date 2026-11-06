namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom;
using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Represents a stream writing to a file. All changes go to a buffer, which
/// replaces the content of the file when the stream is closed. Hence, the
/// file stays untouched if the stream is aborted or closing it fails. As
/// AngleSharp has no streams, the part of WritableStream used with files is
/// provided directly.
/// </summary>
[DomName("FileSystemWritableFileStream")]
public sealed class FileSystemWritableFileStream
{
    private readonly FileSystemFileHandle _handle;
    private readonly MemoryStream _buffer;
    private readonly Object _sync;

    private Boolean _isDone;

    internal FileSystemWritableFileStream(FileSystemFileHandle handle, MemoryStream buffer)
    {
        _handle = handle;
        _buffer = buffer;
        _sync = new Object();
    }

    /// <summary>
    /// Writes the data at the current position, or runs the given command.
    /// </summary>
    /// <param name="data">
    /// The data, e.g., a string, a byte array, or a blob, or a <see cref="WriteParams"/>.
    /// </param>
    /// <returns>The task representing the operation.</returns>
    public async Task Write(Object data)
    {
        EnsureOpen();
        await _handle.EnsureAccessAsync(FileSystemPermissionMode.ReadWrite).ConfigureAwait(false);

        lock (_sync)
        {
            EnsureOpen();

            if (data is WriteParams command)
            {
                Run(command);
            }
            else
            {
                WriteChunk(data);
            }
        }
    }

    /// <summary>
    /// Moves the current position, which may lie behind the end of the file.
    /// </summary>
    /// <param name="position">The new position.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("seek")]
    public Task Seek(Double position) => Write(new WriteParams { Type = WriteCommandType.Seek, Position = position });

    /// <summary>
    /// Resizes the file, filling new space with zeros. The current position
    /// is moved to the new end if it would lie behind it.
    /// </summary>
    /// <param name="size">The new size.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("truncate")]
    public Task Truncate(Double size) => Write(new WriteParams { Type = WriteCommandType.Truncate, Size = size });

    /// <summary>
    /// Replaces the content of the file with the written data and closes the
    /// stream, which is also closed if that fails.
    /// </summary>
    /// <returns>The task completing once the file has been replaced.</returns>
    [DomName("close")]
    public async Task Close()
    {
        lock (_sync)
        {
            EnsureOpen();
            _isDone = true;
        }

        using (_buffer)
        {
            await _handle.EnsureAccessAsync(FileSystemPermissionMode.ReadWrite).ConfigureAwait(false);
            _buffer.Position = 0;
            await _handle.Platform.WriteFileAsync(_handle.Entry, _buffer).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Discards the written data and closes the stream.
    /// </summary>
    /// <param name="reason">The optional reason, which is not used.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("abort")]
    public Task Abort(Object reason = null)
    {
        lock (_sync)
        {
            if (!_isDone)
            {
                _isDone = true;
                _buffer.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    // the spec rejects the missing members with a TypeError
    private void Run(WriteParams command)
    {
        switch (command.Type)
        {
            case WriteCommandType.Write:
                if (command.Position.HasValue)
                {
                    _buffer.Position = ToBufferSize(command.Position.Value);
                }

                WriteChunk(command.Data);
                break;
            case WriteCommandType.Seek:
                _buffer.Position = ToBufferSize(command.Position ?? throw new ArgumentException("Seeking requires a position."));
                break;
            case WriteCommandType.Truncate:
                var position = _buffer.Position;
                var size = ToBufferSize(command.Size ?? throw new ArgumentException("Truncating requires a size."));
                _buffer.SetLength(size);
                _buffer.Position = Math.Min(position, size);
                break;
        }
    }

    private void WriteChunk(Object data)
    {
        // anything else would end up as the result of its ToString, e.g.,
        // "[object Object]" for values of a script that are not converted
        if (data is not (String or Byte[] or ArraySegment<Byte> or IBlob or Stream or IConvertible))
        {
            throw new ArgumentException("Writing requires a string, bytes, or a blob.");
        }

        RequestBody.Serialize(data).Content.CopyTo(_buffer);
    }

    private void EnsureOpen()
    {
        // the spec rejects with a TypeError as the stream is not writable
        if (_isDone)
        {
            throw new InvalidOperationException("The stream has been closed.");
        }
    }

    private static Int64 ToBufferSize(Double value)
    {
        // the spec's [EnforceRange] rejects these with a TypeError
        if (Double.IsNaN(value) || value < 0)
        {
            throw new ArgumentException("The value must not be negative.");
        }

        // the buffer is limited by what a single array can hold
        return value <= Int32.MaxValue ? (Int64)value : throw new DomException(DomError.QuotaExceeded);
    }
}

/// <summary>
/// Defines the forms of write on a writable stream. WebIDL has a single method
/// with a union type here, which script engines resolve via these overloads.
/// </summary>
[DomExposed("FileSystemWritableFileStream")]
public static class FileSystemWritableFileStreamExtensions
{
    /// <summary>
    /// Writes the text at the current position.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="data">The text.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("write")]
    public static Task Write(this FileSystemWritableFileStream stream, String data) => stream.Write((Object)data);

    /// <summary>
    /// Writes the blob at the current position.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="data">The blob.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("write")]
    public static Task Write(this FileSystemWritableFileStream stream, IBlob data) => stream.Write((Object)data);

    /// <summary>
    /// Writes the bytes at the current position.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="data">The bytes.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("write")]
    public static Task Write(this FileSystemWritableFileStream stream, Byte[] data) => stream.Write((Object)data);

    /// <summary>
    /// Runs the given command.
    /// </summary>
    /// <param name="stream">The stream to change.</param>
    /// <param name="command">The command.</param>
    /// <returns>The task representing the operation.</returns>
    [DomName("write")]
    public static Task Write(this FileSystemWritableFileStream stream, WriteParams command) => stream.Write((Object)command);
}

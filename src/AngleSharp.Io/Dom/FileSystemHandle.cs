namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Io.PermissionsApi;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents a handle to an entry of the file system. Handles to the same
/// entry are different objects, which can be compared via
/// <see cref="IsSameEntry"/>.
/// </summary>
[DomName("FileSystemHandle")]
public abstract class FileSystemHandle
{
    internal FileSystemHandle(IFileSystemPlatform platform, FileSystemEntry entry, Permissions permissions)
    {
        Platform = platform;
        Entry = entry;
        Permissions = permissions;
    }

    /// <summary>
    /// Gets the kind of the entry.
    /// </summary>
    [DomName("kind")]
    public abstract FileSystemHandleKind Kind { get; }

    /// <summary>
    /// Gets the name of the entry.
    /// </summary>
    [DomName("name")]
    public String Name => Entry.Name;

    internal IFileSystemPlatform Platform { get; }

    internal FileSystemEntry Entry { get; }

    internal Permissions Permissions { get; }

    /// <summary>
    /// Checks if the given handle represents the same entry.
    /// </summary>
    /// <param name="other">The handle to compare to.</param>
    /// <returns>True if both handles represent the same entry, otherwise false.</returns>
    [DomName("isSameEntry")]
    public Task<Boolean> IsSameEntry(FileSystemHandle other)
    {
        var isSame = other != null &&
            other.Kind == Kind &&
            ReferenceEquals(other.Platform, Platform) &&
            String.Equals(other.Entry.Id, Entry.Id, StringComparison.Ordinal);

        return Task.FromResult(isSame);
    }

    /// <summary>
    /// Gets the state of the permission for accessing the entry.
    /// </summary>
    /// <param name="descriptor">The access to query; by default, this is reading.</param>
    /// <returns>The state, which is granted if there is no policy.</returns>
    [DomName("queryPermission")]
    public Task<PermissionState> QueryPermission(FileSystemHandlePermissionDescriptor descriptor = null)
    {
        var mode = descriptor?.Mode ?? FileSystemPermissionMode.Read;

        if (Permissions is null || IncludedInWrite(mode))
        {
            return Task.FromResult(PermissionState.Granted);
        }

        return Task.FromResult(Permissions.GetState(GetName(mode)));
    }

    /// <summary>
    /// Requests the permission for accessing the entry.
    /// </summary>
    /// <param name="descriptor">The access to request; by default, this is reading.</param>
    /// <returns>The state after the request.</returns>
    [DomName("requestPermission")]
    public Task<PermissionState> RequestPermission(FileSystemHandlePermissionDescriptor descriptor = null)
    {
        var mode = descriptor?.Mode ?? FileSystemPermissionMode.Read;

        if (Permissions is null || IncludedInWrite(mode))
        {
            return Task.FromResult(PermissionState.Granted);
        }

        return Permissions.RequestAsync(GetName(mode));
    }

    internal async Task EnsureAccessAsync(FileSystemPermissionMode mode)
    {
        if (await RequestPermissionFor(mode).ConfigureAwait(false) != PermissionState.Granted)
        {
            throw new AngleSharp.Dom.DomException(Permissions.NotAllowedError);
        }
    }

    internal static String GetName(FileSystemPermissionMode mode) =>
        mode == FileSystemPermissionMode.ReadWrite ? PermissionNames.FileSystemWrite : PermissionNames.FileSystemRead;

    private Task<PermissionState> RequestPermissionFor(FileSystemPermissionMode mode) =>
        RequestPermission(new FileSystemHandlePermissionDescriptor { Mode = mode });

    // readwrite is a superset of read; a separate state for reading would
    // lock a document out of the file it may overwrite
    private Boolean IncludedInWrite(FileSystemPermissionMode mode) =>
        mode == FileSystemPermissionMode.Read && Permissions.GetState(PermissionNames.FileSystemWrite) == PermissionState.Granted;
}

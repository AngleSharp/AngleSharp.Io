namespace AngleSharp.Io.Dom;

using System;

/// <summary>
/// The arguments of a change of a permission policy.
/// </summary>
public sealed class PermissionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates new arguments for the given change.
    /// </summary>
    /// <param name="origin">The affected origin, or empty if all origins are affected.</param>
    /// <param name="name">The name of the changed permission.</param>
    public PermissionChangedEventArgs(String origin, String name)
    {
        Origin = origin ?? String.Empty;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the affected origin, or empty if all origins are affected.
    /// </summary>
    public String Origin { get; }

    /// <summary>
    /// Gets the name of the changed permission.
    /// </summary>
    public String Name { get; }
}

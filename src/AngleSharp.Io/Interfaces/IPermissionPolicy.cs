namespace AngleSharp.Io.Dom;

using System;
using System.Threading.Tasks;

/// <summary>
/// Represents the policy deciding about the permissions of the origins.
/// It backs navigator.permissions and is consulted by every API using a
/// powerful feature, e.g., the clipboard or the geolocation.
/// </summary>
public interface IPermissionPolicy
{
    /// <summary>
    /// Raised when the state of a permission changed.
    /// </summary>
    event EventHandler<PermissionChangedEventArgs> Changed;

    /// <summary>
    /// Checks if the permission with the given name is known.
    /// </summary>
    /// <param name="name">The name of the permission.</param>
    /// <returns>True if the permission is known, otherwise false.</returns>
    Boolean IsSupported(String name);

    /// <summary>
    /// Gets the current state of the permission without asking anyone.
    /// </summary>
    /// <param name="origin">The origin using the feature, or empty for an opaque origin.</param>
    /// <param name="name">The name of the permission.</param>
    /// <returns>The current state.</returns>
    PermissionState Query(String origin, String name);

    /// <summary>
    /// Requests the permission, which resolves a prompt state to either
    /// granted or denied.
    /// </summary>
    /// <param name="origin">The origin using the feature, or empty for an opaque origin.</param>
    /// <param name="name">The name of the permission.</param>
    /// <returns>The state after the request, which is never prompt.</returns>
    Task<PermissionState> RequestAsync(String origin, String name);
}

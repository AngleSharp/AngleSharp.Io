namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom;
using System;
using System.Threading.Tasks;

/// <summary>
/// Represents the Permissions API, which is also the single place where
/// other APIs check if they may use a powerful feature.
/// </summary>
[DomName("Permissions")]
public sealed class Permissions
{
    // AngleSharp's DomError has no entry for it; the string constructor
    // makes it at least appear under the name used by the spec
    internal static readonly String NotAllowedError = "NotAllowedError";

    private readonly IPermissionPolicy _policy;
    private readonly Func<String> _getOrigin;

    /// <summary>
    /// Creates new permissions backed by the given policy.
    /// </summary>
    /// <param name="policy">The policy deciding about the permissions.</param>
    /// <param name="getOrigin">The callback for resolving the current origin.</param>
    public Permissions(IPermissionPolicy policy, Func<String> getOrigin)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _getOrigin = getOrigin ?? throw new ArgumentNullException(nameof(getOrigin));
    }

    /// <summary>
    /// Gets the policy deciding about the permissions.
    /// </summary>
    public IPermissionPolicy Policy => _policy;

    /// <summary>
    /// Queries the state of the described permission.
    /// </summary>
    /// <param name="descriptor">The descriptor naming the permission.</param>
    /// <returns>The status, which follows later changes of the state.</returns>
    [DomName("query")]
    public Task<PermissionStatus> Query(PermissionDescriptor descriptor)
    {
        var name = descriptor?.Name;

        // the spec rejects with a TypeError as unknown names fail the
        // conversion to the PermissionName enumeration
        if (String.IsNullOrEmpty(name) || !_policy.IsSupported(name))
        {
            return Task.FromException<PermissionStatus>(new ArgumentException($"The permission '{name}' is not supported.", nameof(descriptor)));
        }

        var status = new PermissionStatus(_policy, GetOrigin(), name);
        return Task.FromResult(status);
    }

    /// <summary>
    /// Gets the current state of the permission for the current origin
    /// without asking anyone.
    /// </summary>
    /// <param name="name">The name of the permission.</param>
    /// <returns>The current state.</returns>
    public PermissionState GetState(String name) => _policy.Query(GetOrigin(), name);

    /// <summary>
    /// Requests the permission for the current origin, as done by an API
    /// before using the feature.
    /// </summary>
    /// <param name="name">The name of the permission.</param>
    /// <returns>The state after the request, which is never prompt.</returns>
    public async Task<PermissionState> RequestAsync(String name)
    {
        var origin = GetOrigin();
        var state = _policy.Query(origin, name);

        if (state == PermissionState.Prompt)
        {
            state = await _policy.RequestAsync(origin, name).ConfigureAwait(false);
        }

        // a policy leaving it at prompt did not get an answer, which must
        // not unlock the feature
        return state == PermissionState.Granted ? state : PermissionState.Denied;
    }

    /// <summary>
    /// Requests the permission for the current origin and throws a
    /// NotAllowedError if it is not granted.
    /// </summary>
    /// <param name="name">The name of the permission.</param>
    /// <returns>The task completing once the permission is granted.</returns>
    public async Task EnsureGrantedAsync(String name)
    {
        var state = await RequestAsync(name).ConfigureAwait(false);

        if (state != PermissionState.Granted)
        {
            throw new DomException(NotAllowedError);
        }
    }

    private String GetOrigin() => _getOrigin.Invoke() ?? String.Empty;
}

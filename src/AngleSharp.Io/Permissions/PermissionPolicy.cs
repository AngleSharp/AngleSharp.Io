namespace AngleSharp.Io.PermissionsApi;

using AngleSharp.Dom;
using AngleSharp.Io.Dom;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Answers a prompt for a permission in place of the user.
/// </summary>
/// <param name="origin">The origin requesting the permission, or empty for an opaque origin.</param>
/// <param name="name">The name of the permission.</param>
/// <returns>The answer; prompt counts as a dismissed prompt.</returns>
public delegate Task<PermissionState> PermissionPromptHandler(String origin, String name);

/// <summary>
/// Represents the default permission policy keeping the states in memory.
/// The state of a permission is taken from the origin's entry, then from
/// the entry for all origins, and finally from the default state.
/// </summary>
public sealed class PermissionPolicy : IPermissionPolicy
{
    private readonly Object _sync;
    private readonly HashSet<String> _names;
    private readonly Dictionary<String, PermissionState> _allOrigins;
    private readonly Dictionary<String, Dictionary<String, PermissionState>> _origins;

    /// <summary>
    /// Creates a new permission policy where every permission requires a
    /// prompt. Without a <see cref="PromptHandler"/> such a prompt is
    /// treated as dismissed, i.e., the feature cannot be used.
    /// </summary>
    public PermissionPolicy()
        : this(PermissionState.Prompt)
    {
    }

    /// <summary>
    /// Creates a new permission policy using the given default state.
    /// </summary>
    /// <param name="defaultState">The state of permissions without an entry.</param>
    public PermissionPolicy(PermissionState defaultState)
    {
        _sync = new Object();
        _names = new HashSet<String>(PermissionNames.All, StringComparer.Ordinal);
        _allOrigins = new Dictionary<String, PermissionState>(StringComparer.Ordinal);
        _origins = new Dictionary<String, Dictionary<String, PermissionState>>(StringComparer.Ordinal);
        DefaultState = defaultState;
    }

    /// <inheritdoc />
    public event EventHandler<PermissionChangedEventArgs> Changed;

    /// <summary>
    /// Gets the state of permissions without an entry.
    /// </summary>
    public PermissionState DefaultState { get; }

    /// <summary>
    /// Gets or sets the handler answering prompts. An answer other than
    /// prompt is remembered for the origin. By default, this is null,
    /// which means that prompts are dismissed.
    /// </summary>
    public PermissionPromptHandler PromptHandler { get; set; }

    /// <summary>
    /// Makes the permission with the given name known, e.g., for a feature
    /// provided by another library.
    /// </summary>
    /// <param name="name">The name of the permission.</param>
    public void Register(String name)
    {
        if (String.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The name of the permission is required.", nameof(name));
        }

        lock (_sync)
        {
            _names.Add(name);
        }
    }

    /// <inheritdoc />
    public Boolean IsSupported(String name)
    {
        if (String.IsNullOrEmpty(name))
        {
            return false;
        }

        lock (_sync)
        {
            return _names.Contains(name);
        }
    }

    /// <summary>
    /// Sets the state of the permission for all origins. Entries of
    /// single origins take precedence.
    /// </summary>
    /// <param name="name">The name of the permission.</param>
    /// <param name="state">The new state.</param>
    public void Set(String name, PermissionState state)
    {
        lock (_sync)
        {
            EnsureSupported(name);
            _allOrigins[name] = state;
        }

        Changed?.Invoke(this, new PermissionChangedEventArgs(String.Empty, name));
    }

    /// <summary>
    /// Sets the state of the permission for the given origin.
    /// </summary>
    /// <param name="origin">The origin, e.g., "https://example.com".</param>
    /// <param name="name">The name of the permission.</param>
    /// <param name="state">The new state.</param>
    public void Set(String origin, String name, PermissionState state)
    {
        var key = NormalizeOrigin(origin);

        if (key.Length == 0)
        {
            throw new ArgumentException("An origin is required.", nameof(origin));
        }

        lock (_sync)
        {
            EnsureSupported(name);

            if (!_origins.TryGetValue(key, out var states))
            {
                states = new Dictionary<String, PermissionState>(StringComparer.Ordinal);
                _origins[key] = states;
            }

            states[name] = state;
        }

        Changed?.Invoke(this, new PermissionChangedEventArgs(key, name));
    }

    /// <inheritdoc />
    public PermissionState Query(String origin, String name)
    {
        var key = NormalizeOrigin(origin);

        lock (_sync)
        {
            if (_origins.TryGetValue(key, out var states) && states.TryGetValue(name, out var state))
            {
                return state;
            }

            if (_allOrigins.TryGetValue(name, out state))
            {
                return state;
            }

            return DefaultState;
        }
    }

    /// <inheritdoc />
    public async Task<PermissionState> RequestAsync(String origin, String name)
    {
        var state = Query(origin, name);

        if (state != PermissionState.Prompt)
        {
            return state;
        }

        var handler = PromptHandler;

        if (handler is null)
        {
            return PermissionState.Denied;
        }

        var answer = await handler.Invoke(NormalizeOrigin(origin), name).ConfigureAwait(false);

        if (answer == PermissionState.Prompt)
        {
            return PermissionState.Denied;
        }

        // opaque origins cannot be told apart, so their answers are not
        // remembered and every use prompts again
        if (NormalizeOrigin(origin).Length > 0)
        {
            Set(origin, name, answer);
        }

        return answer;
    }

    private void EnsureSupported(String name)
    {
        if (String.IsNullOrEmpty(name) || !_names.Contains(name))
        {
            throw new ArgumentException($"The permission '{name}' is not registered.", nameof(name));
        }
    }

    private static String NormalizeOrigin(String origin)
    {
        if (String.IsNullOrEmpty(origin))
        {
            return String.Empty;
        }

        var url = new Url(origin);

        if (url.IsInvalid || url.IsRelative)
        {
            return origin;
        }

        return url.Origin ?? origin;
    }
}

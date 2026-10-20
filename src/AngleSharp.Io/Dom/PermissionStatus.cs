namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using System;

/// <summary>
/// Represents the status of a permission for the origin it was queried
/// for. The state follows the changes of the policy.
/// </summary>
[DomName("PermissionStatus")]
public sealed class PermissionStatus : EventTarget
{
    private readonly IPermissionPolicy _policy;
    private readonly String _origin;
    private readonly String _name;

    private PermissionState _state;

    internal PermissionStatus(IPermissionPolicy policy, String origin, String name)
    {
        _policy = policy;
        _origin = origin;
        _name = name;
        _state = policy.Query(origin, name);
        Subscription.Attach(policy, this);
    }

    /// <summary>
    /// Gets the name of the permission.
    /// </summary>
    [DomName("name")]
    public String Name => _name;

    /// <summary>
    /// Gets the current state of the permission.
    /// </summary>
    [DomName("state")]
    public PermissionState State => _state;

    /// <summary>
    /// Adds or removes the handler for the change event.
    /// </summary>
    [DomName("onchange")]
    public event DomEventHandler Changed
    {
        add { AddEventListener(EventNames.Change, value, false); }
        remove { RemoveEventListener(EventNames.Change, value, false); }
    }

    private void OnPolicyChanged(PermissionChangedEventArgs e)
    {
        var affected = String.Equals(e.Name, _name, StringComparison.Ordinal) &&
            (e.Origin.Length == 0 || String.Equals(e.Origin, _origin, StringComparison.Ordinal));

        if (affected)
        {
            var state = _policy.Query(_origin, _name);

            if (state != _state)
            {
                _state = state;
                Dispatch(new Event(EventNames.Change));
            }
        }
    }

    // the policy usually outlives the documents, so subscribing the status
    // directly would keep every status ever queried alive; the weak
    // subscription detaches itself once its status has been collected
    private sealed class Subscription
    {
        private readonly IPermissionPolicy _policy;
        private readonly WeakReference<PermissionStatus> _status;

        private Subscription(IPermissionPolicy policy, PermissionStatus status)
        {
            _policy = policy;
            _status = new WeakReference<PermissionStatus>(status);
        }

        public static void Attach(IPermissionPolicy policy, PermissionStatus status)
        {
            var subscription = new Subscription(policy, status);
            policy.Changed += subscription.OnChanged;
        }

        private void OnChanged(Object sender, PermissionChangedEventArgs e)
        {
            if (_status.TryGetTarget(out var status))
            {
                status.OnPolicyChanged(e);
            }
            else
            {
                _policy.Changed -= OnChanged;
            }
        }
    }
}

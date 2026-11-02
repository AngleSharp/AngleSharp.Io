namespace AngleSharp.Io.BackgroundSyncApi;

using AngleSharp.Dom;
using AngleSharp.Io.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Holds the tasks of a registration for one of the sync managers, which only
/// differ in the kind of their tasks and the permission protecting them.
/// </summary>
sealed class SyncTaskList
{
    // the spec leaves it to the user agent; browsers settled on three attempts
    private const Int32 MaxAttempts = 3;

    private readonly ISyncPlatform _platform;
    private readonly String _permission;
    private readonly List<SyncTask> _tasks;

    public SyncTaskList(ServiceWorkerRegistration registration, ISyncPlatform platform, SyncTaskKind kind, String permission)
    {
        Registration = registration;
        Kind = kind;
        _platform = platform;
        _permission = permission;
        _tasks = new List<SyncTask>();
    }

    public ServiceWorkerRegistration Registration { get; }

    public SyncTaskKind Kind { get; }

    public async Task RegisterAsync(String tag, UInt64 minInterval)
    {
        // registrations are tied to a secure origin, which the container
        // ensures, but not the registry that is also available to the host
        if (!ServiceWorkerContainer.IsPotentiallyTrustworthy(new Url(Registration.Scope)))
        {
            throw new DomException(DomError.Security);
        }

        if (Registration.Active is null)
        {
            throw new DomException(DomError.InvalidState);
        }

        var permissions = Registration.Permissions;

        if (permissions != null)
        {
            await permissions.EnsureGrantedAsync(_permission).ConfigureAwait(false);
        }

        var name = tag ?? String.Empty;
        var created = default(SyncTask);
        var rescheduled = default(SyncTask);

        lock (_tasks)
        {
            var current = Find(name);

            if (current is null)
            {
                created = new SyncTask(this, name, minInterval);
                _tasks.Add(created);
            }
            else if (Kind == SyncTaskKind.Periodic)
            {
                // a task being fired gets scheduled with the new value anyway
                if (current.MinInterval != minInterval)
                {
                    current.MinInterval = minInterval;
                    rescheduled = current.State == SyncTaskState.Pending ? current : null;
                }
            }
            else if (current.State == SyncTaskState.Firing)
            {
                // something new may have to be synced that the running
                // attempt does not see, hence it fires once more afterwards
                current.State = SyncTaskState.ReregisteredWhileFiring;
            }
        }

        if (rescheduled != null)
        {
            await _platform.CancelAsync(rescheduled).ConfigureAwait(false);
            await _platform.ScheduleAsync(rescheduled).ConfigureAwait(false);
        }
        else if (created != null)
        {
            try
            {
                await _platform.ScheduleAsync(created).ConfigureAwait(false);
            }
            catch
            {
                // a task the platform does not know about would never fire
                // while blocking the tag
                Remove(created);
                throw;
            }
        }
    }

    public String[] GetTags()
    {
        lock (_tasks)
        {
            return _tasks.Select(task => task.Tag).ToArray();
        }
    }

    public async Task UnregisterAsync(String tag)
    {
        SyncTask task;

        lock (_tasks)
        {
            task = Find(tag ?? String.Empty);
        }

        if (task != null && Remove(task))
        {
            await _platform.CancelAsync(task).ConfigureAwait(false);
        }
    }

    public async Task TerminateAsync()
    {
        SyncTask[] tasks;

        lock (_tasks)
        {
            tasks = _tasks.ToArray();
        }

        foreach (var task in tasks.Where(Remove))
        {
            try
            {
                await _platform.CancelAsync(task).ConfigureAwait(false);
            }
            catch
            {
                // the worker is gone, so the task would not fire anymore
            }
        }
    }

    public async Task<Boolean> FireAsync(SyncTask task)
    {
        var worker = Registration.Active;

        lock (_tasks)
        {
            if (task.State != SyncTaskState.Pending || worker?.State != ServiceWorkerState.Activated)
            {
                return false;
            }

            task.State = SyncTaskState.Firing;
            task.LastChance = Kind == SyncTaskKind.OneOff && task.FailedAttempts >= MaxAttempts - 1;
        }

        var success = true;

        try
        {
            await _platform.DispatchAsync(worker, task).ConfigureAwait(false);
        }
        catch
        {
            success = false;
        }

        var schedule = false;

        lock (_tasks)
        {
            if (task.State == SyncTaskState.ReregisteredWhileFiring)
            {
                task.FailedAttempts = 0;
                schedule = true;
            }
            else if (task.State == SyncTaskState.Firing)
            {
                task.FailedAttempts = success ? 0 : task.FailedAttempts + 1;
                schedule = Kind == SyncTaskKind.Periodic || (!success && !task.LastChance);

                if (!schedule)
                {
                    _tasks.Remove(task);
                    task.State = SyncTaskState.Removed;
                }
            }

            if (schedule)
            {
                task.State = SyncTaskState.Pending;
                task.LastChance = false;
            }
        }

        if (schedule)
        {
            await _platform.ScheduleAsync(task).ConfigureAwait(false);
        }

        return success;
    }

    private SyncTask Find(String tag) => _tasks.FirstOrDefault(task => String.Equals(task.Tag, tag, StringComparison.Ordinal));

    private Boolean Remove(SyncTask task)
    {
        lock (_tasks)
        {
            task.State = SyncTaskState.Removed;
            return _tasks.Remove(task);
        }
    }
}

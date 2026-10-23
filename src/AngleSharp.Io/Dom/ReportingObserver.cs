namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Browser;
    using AngleSharp.Dom;
    using AngleSharp.Io.Reporting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The callback receiving the reports of a reporting observer.
    /// </summary>
    /// <param name="reports">The observed reports.</param>
    /// <param name="observer">The observer.</param>
    [DomName("ReportingObserverCallback")]
    public delegate void ReportingObserverCallback(Report[] reports, ReportingObserver observer);

    /// <summary>
    /// Represents the ReportingObserver interface, which observes the
    /// reports queued for a window. As in browsers, the callback is invoked
    /// in a task of the context's event loop; without an event loop it is
    /// invoked right away.
    /// </summary>
    [DomName("ReportingObserver")]
    [DomExposed("Window")]
    public sealed class ReportingObserver
    {
        private readonly IWindow _window;
        private readonly ReportingObserverCallback _callback;
        private readonly HashSet<String> _types;
        private readonly Boolean _buffered;
        private readonly List<Report> _records;

        /// <summary>
        /// Creates a new reporting observer for all report types.
        /// </summary>
        /// <param name="window">The window to observe.</param>
        /// <param name="callback">The callback receiving the reports.</param>
        [DomConstructor]
        public ReportingObserver(IWindow window, ReportingObserverCallback callback)
            : this(window, callback, null)
        {
        }

        /// <summary>
        /// Creates a new reporting observer.
        /// </summary>
        /// <param name="window">The window to observe.</param>
        /// <param name="callback">The callback receiving the reports.</param>
        /// <param name="options">The options of the observer.</param>
        [DomConstructor]
        public ReportingObserver(IWindow window, ReportingObserverCallback callback, ReportingObserverOptions options)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _types = options?.Types is null ? null : new HashSet<String>(options.Types, StringComparer.Ordinal);
            _buffered = options?.Buffered ?? false;
            _records = new List<Report>();
        }

        /// <summary>
        /// Starts observing the reports of the window. With the buffered
        /// option, the reports queued before are delivered right away.
        /// </summary>
        [DomName("observe")]
        public void Observe()
        {
            var buffered = WindowReports.For(_window).Register(this);

            if (_buffered)
            {
                foreach (var report in buffered)
                {
                    Add(report);
                }
            }
        }

        /// <summary>
        /// Stops observing the reports of the window.
        /// </summary>
        [DomName("disconnect")]
        public void Disconnect()
        {
            WindowReports.For(_window).Unregister(this);
        }

        /// <summary>
        /// Takes the reports that have not been delivered to the callback yet.
        /// </summary>
        /// <returns>The pending reports.</returns>
        [DomName("takeRecords")]
        public Report[] TakeRecords()
        {
            lock (_records)
            {
                var records = _records.ToArray();
                _records.Clear();
                return records;
            }
        }

        internal void Add(Report report)
        {
            if (_types is not null && !_types.Contains(report.Type))
            {
                return;
            }

            Boolean first;

            lock (_records)
            {
                first = _records.Count == 0;
                _records.Add(report);
            }

            // like the spec, only the first report schedules the callback,
            // which then receives all reports added until the task runs
            if (first)
            {
                var loop = _window.Document?.Context?.GetService<IEventLoop>();

                if (loop is null)
                {
                    Notify();
                }
                else
                {
                    loop.Enqueue(Notify, TaskPriority.Normal);
                }
            }
        }

        private void Notify()
        {
            var records = TakeRecords();

            if (records.Length > 0)
            {
                _callback.Invoke(records, this);
            }
        }
    }

    /// <summary>
    /// Represents the options of a reporting observer.
    /// </summary>
    [DomName("ReportingObserverOptions")]
    public sealed class ReportingObserverOptions
    {
        /// <summary>
        /// Gets or sets the report types to observe, or null for all types.
        /// </summary>
        [DomName("types")]
        public String[] Types { get; set; }

        /// <summary>
        /// Gets or sets if reports queued before observing are delivered.
        /// </summary>
        [DomName("buffered")]
        public Boolean Buffered { get; set; }
    }

    /// <summary>
    /// The report buffer and the observers of a window.
    /// </summary>
    sealed class WindowReports
    {
        // the size of the report buffer used by browsers
        private const Int32 BufferSize = 100;

        private static readonly ConditionalWeakTable<IWindow, WindowReports> Instances = new ConditionalWeakTable<IWindow, WindowReports>();

        private readonly List<Report> _buffer = new List<Report>();
        private readonly List<ReportingObserver> _observers = new List<ReportingObserver>();

        public static WindowReports For(IWindow window) => Instances.GetValue(window, _ => new WindowReports());

        public Report[] Register(ReportingObserver observer)
        {
            lock (_buffer)
            {
                if (!_observers.Contains(observer))
                {
                    _observers.Add(observer);
                }

                return _buffer.ToArray();
            }
        }

        public void Unregister(ReportingObserver observer)
        {
            lock (_buffer)
            {
                _observers.Remove(observer);
            }
        }

        public void Add(Report report)
        {
            ReportingObserver[] observers;

            lock (_buffer)
            {
                _buffer.Add(report);

                if (_buffer.Count > BufferSize)
                {
                    _buffer.RemoveAt(0);
                }

                observers = _observers.ToArray();
            }

            foreach (var observer in observers)
            {
                observer.Add(report);
            }
        }
    }
}

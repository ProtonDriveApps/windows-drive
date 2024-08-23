// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using ProtonDrive.Sync.Windows.FileSystem.Internal;

namespace ProtonDrive.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Listens to the system directory change notifications and
/// raises events when a directory or file within a directory changes.
/// Supported starting from Windows 10, version 1709 (desktop apps only).
/// </summary>
public partial class FileSystemExtendedWatcher : Component, ISupportInitialize
{
    private const NotifyFilters DefaultNotifyFilters = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

    private const int NotifyFiltersValidMask = (int)(NotifyFilters.Attributes |
                                                     NotifyFilters.CreationTime |
                                                     NotifyFilters.DirectoryName |
                                                     NotifyFilters.FileName |
                                                     NotifyFilters.LastAccess |
                                                     NotifyFilters.LastWrite |
                                                     NotifyFilters.Security |
                                                     NotifyFilters.Size);

    // Filters collection
    private readonly NormalizedFilterCollection _filters = new NormalizedFilterCollection();

    // Directory being monitored
    private string _directory;

    // The watch filter for the API call.
    private NotifyFilters _notifyFilters = DefaultNotifyFilters;

    // Flag to watch subtree of this directory
    private bool _includeSubdirectories;

    // Flag to note whether we are attached to the thread pool and responding to changes
    private bool _enabled;

    // Are we in init?
    private bool _initializing;

    // Buffer size
    private uint _internalBufferSize = 8192;

    // Share mode
    private FileShare _shareMode = FileShare.Read | FileShare.Delete | FileShare.Write;

    // Used for synchronization
    private bool _disposed;

    // Used for accumulating event args into a single batch
    private List<RenamedExtendedEventArgs>? _batchedEventArgs;

    // Event handlers
    private FileSystemExtendedEventHandler? _onChangedHandler;
    private FileSystemExtendedEventHandler? _onCreatedHandler;
    private FileSystemExtendedEventHandler? _onDeletedHandler;
    private RenamedExtendedEventHandler? _onRenamedHandler;
    private ErrorExtendedEventHandler? _onErrorHandler;
    private FileSystemExtendedMultiEventHandler? _onNextEventsHandler;

#if DEBUG
    static FileSystemExtendedWatcher()
    {
        int notifyFiltersValidMask = 0;
        foreach (int enumValue in Enum.GetValues(typeof(NotifyFilters)))
            notifyFiltersValidMask |= enumValue;
        Debug.Assert(notifyFiltersValidMask == NotifyFiltersValidMask, "The NotifyFilters enum has changed. The NotifyFiltersValidMask must be updated to reflect the values of the NotifyFilters enum.");
    }
#endif

    /// <summary>
    ///    Initializes a new instance of the <see cref='FileSystemExtendedWatcher'/> class.
    /// </summary>
    public FileSystemExtendedWatcher()
    {
        _directory = string.Empty;
    }

    /// <summary>
    ///    Initializes a new instance of the <see cref='FileSystemExtendedWatcher'/> class,
    ///    given the specified directory to monitor.
    /// </summary>
    public FileSystemExtendedWatcher(string path)
    {
        CheckPathValidity(path);
        _directory = path;
    }

    /// <summary>
    ///    Initializes a new instance of the <see cref='FileSystemExtendedWatcher'/> class,
    ///    given the specified directory and type of files to monitor.
    /// </summary>
    public FileSystemExtendedWatcher(string path, string filter)
    {
        CheckPathValidity(path);
        _directory = path;
        Filter = filter;
    }

    /// <summary>
    ///    Gets or sets the type of changes to watch for.
    /// </summary>
    public NotifyFilters NotifyFilter
    {
        get
        {
            return _notifyFilters;
        }
        set
        {
            if (((int)value & ~NotifyFiltersValidMask) != 0)
                throw new ArgumentException($"The value of argument '{nameof(value)}' ({(int)value}) is invalid for Enum type '{nameof(NotifyFilters)}'.");

            if (_notifyFilters != value)
            {
                _notifyFilters = value;

                Restart();
            }
        }
    }

    public Collection<string> Filters => _filters;

    /// <summary>
    ///    Gets or sets a value indicating whether the component is enabled.
    /// </summary>
    public bool EnableRaisingEvents
    {
        get
        {
            return _enabled;
        }
        set
        {
            if (_enabled == value)
            {
                return;
            }

            if (IsSuspended())
            {
                _enabled = value; // Alert the Component to start watching for events when EndInit is called.
            }
            else
            {
                if (value)
                {
                    StartRaisingEventsIfNotDisposed(); // will set _enabled to true once successfully started
                }
                else
                {
                    StopRaisingEvents(); // will set _enabled to false
                }
            }
        }
    }

    /// <summary>
    ///    Gets or sets the filter string, used to determine what files are monitored in a directory.
    /// </summary>
    public string Filter
    {
        get
        {
            return Filters.Count == 0 ? "*" : Filters[0];
        }
        set
        {
            Filters.Clear();
            Filters.Add(value);
        }
    }

    /// <summary>
    ///    Gets or sets a value indicating whether subdirectories within the specified path should be monitored.
    /// </summary>
    public bool IncludeSubdirectories
    {
        get
        {
            return _includeSubdirectories;
        }
        set
        {
            if (_includeSubdirectories != value)
            {
                _includeSubdirectories = value;

                Restart();
            }
        }
    }

    /// <summary>
    ///    Gets or sets the size of the internal buffer.
    /// </summary>
    public int InternalBufferSize
    {
        get
        {
            return (int)_internalBufferSize;
        }
        set
        {
            if (_internalBufferSize != value)
            {
                if (value < 4096)
                {
                    _internalBufferSize = 4096;
                }
                else
                {
                    _internalBufferSize = (uint)value;
                }

                Restart();
            }
        }
    }

    public FileShare ShareMode
    {
        get
        {
            return _shareMode;
        }
        set
        {
            if (_shareMode != value)
            {
                _shareMode = value;
                Restart();
            }
        }
    }

    /// <summary>Allocates a buffer of the requested internal buffer size.</summary>
    /// <returns>The allocated buffer.</returns>
    private byte[] AllocateBuffer()
    {
        try
        {
            return new byte[_internalBufferSize];
        }
        catch (OutOfMemoryException)
        {
            throw new OutOfMemoryException(
                $"The specified buffer size is too large. FileSystemWatcher cannot allocate {_internalBufferSize} bytes for the internal buffer.");
        }
    }

    /// <summary>
    ///    Gets or sets the path of the directory to watch.
    /// </summary>
    public string Path
    {
        get
        {
            return _directory;
        }
        set
        {
            value = string.IsNullOrEmpty(value) ? string.Empty : value;
            if (!string.Equals(_directory, value, PathInternal.StringComparison))
            {
                if (value.Length == 0)
                    throw new ArgumentException($"The directory name '{value}' is invalid.", nameof(Path));

                if (!Directory.Exists(value))
                    throw new FileNotFoundException($"The directory '{value}' does not exist.", nameof(Path));

                _directory = value;
                Restart();
            }
        }
    }

    /// <summary>
    ///    Occurs when a file or directory in the specified <see cref='FileSystemExtendedWatcher.Path'/> is changed.
    /// </summary>
    public event FileSystemExtendedEventHandler? Changed
    {
        add
        {
            _onChangedHandler += value;
        }
        remove
        {
            _onChangedHandler -= value;
        }
    }

    /// <summary>
    ///    Occurs when a file or directory in the specified <see cref='FileSystemExtendedWatcher.Path'/> is created.
    /// </summary>
    public event FileSystemExtendedEventHandler? Created
    {
        add
        {
            _onCreatedHandler += value;
        }
        remove
        {
            _onCreatedHandler -= value;
        }
    }

    /// <summary>
    ///    Occurs when a file or directory in the specified <see cref='FileSystemExtendedWatcher.Path'/> is deleted.
    /// </summary>
    public event FileSystemExtendedEventHandler? Deleted
    {
        add
        {
            _onDeletedHandler += value;
        }
        remove
        {
            _onDeletedHandler -= value;
        }
    }

    /// <summary>
    ///    Occurs when the internal buffer overflows.
    /// </summary>
    public event ErrorExtendedEventHandler? Error
    {
        add
        {
            _onErrorHandler += value;
        }
        remove
        {
            _onErrorHandler -= value;
        }
    }

    /// <summary>
    ///    Occurs when a file or directory in the specified <see cref='FileSystemExtendedWatcher.Path'/>
    ///    is renamed.
    /// </summary>
    public event RenamedExtendedEventHandler? Renamed
    {
        add
        {
            _onRenamedHandler += value;
        }
        remove
        {
            _onRenamedHandler -= value;
        }
    }

    /// <summary>
    ///    Occurs when file(s) or directory(ies) in the specified <see cref='FileSystemExtendedWatcher.Path'/> are
    ///    created, changed, renamed or deleted.
    /// </summary>
    public event FileSystemExtendedMultiEventHandler? NextEvents
    {
        add
        {
            _onNextEventsHandler += value;
        }
        remove
        {
            _onNextEventsHandler -= value;
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                //Stop raising events cleans up managed and
                //unmanaged resources.
                StopRaisingEvents();

                // Clean up managed resources
                _onChangedHandler = null;
                _onCreatedHandler = null;
                _onDeletedHandler = null;
                _onRenamedHandler = null;
                _onErrorHandler = null;
                _onNextEventsHandler = null;
            }
            else
            {
                FinalizeDispose();
            }
        }
        finally
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }

    private static void CheckPathValidity(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        // Early check for directory parameter so that an exception can be thrown as early as possible.
        if (path.Length == 0)
            throw new ArgumentException($"The directory name '{path}' is invalid.", nameof(path));

        if (!Directory.Exists(path))
            throw new ArgumentException($"The directory name '{path}' does not exist.", nameof(path));
    }

    /// <summary>
    /// Sees if the name given matches the name filter we have.
    /// </summary>
    private bool MatchPattern(ReadOnlySpan<char> relativePath)
    {
        ReadOnlySpan<char> name = System.IO.Path.GetFileName(relativePath);
        if (name.Length == 0)
            return false;

        string[] filters = _filters.GetFilters();
        if (filters.Length == 0)
            return true;

        foreach (string filter in filters)
        {
            if (FileSystemName.MatchesSimpleExpression(filter, name, ignoreCase: !PathInternal.IsCaseSensitive))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Raises the event to each handler in the list.
    /// </summary>
    private void NotifyInternalBufferOverflowEvent()
    {
        _onErrorHandler?.Invoke(this, new ErrorExtendedEventArgs(
            new InternalBufferOverflowException($"Too many changes at once in directory:{_directory}.")));
    }

    /// <summary>
    /// Raises the event to each handler in the list.
    /// </summary>
    private void NotifyRenameEventArgs(
        WatcherChangeTypes action,
        ReadOnlySpan<char> name,
        ReadOnlySpan<char> oldName,
        FileExtendedInfo extendedInfo,
        long oldParentFileId)
    {
        // filter if there's no handler or neither new name or old name match a specified pattern
        RenamedExtendedEventHandler? handler = _onRenamedHandler;
        if (handler == null && _batchedEventArgs == null)
        {
            return;
        }

        if (MatchPattern(name) || MatchPattern(oldName))
        {
            var eventArgs = new RenamedExtendedEventArgs(
                action,
                _directory,
                name.IsEmpty ? null : name.ToString(),
                oldName.IsEmpty ? null : oldName.ToString(),
                extendedInfo,
                oldParentFileId);

            handler?.Invoke(this, eventArgs);

            if (_batchedEventArgs != null)
            {
                BatchEvent(eventArgs);
            }
        }
    }

    private FileSystemExtendedEventHandler? GetHandler(WatcherChangeTypes changeType)
    {
        switch (changeType)
        {
            case WatcherChangeTypes.Created:
                return _onCreatedHandler;
            case WatcherChangeTypes.Deleted:
                return _onDeletedHandler;
            case WatcherChangeTypes.Changed:
                return _onChangedHandler;
        }

        Debug.Fail("Unknown FileSystemEvent change type! Value: " + changeType);
        return null;
    }

    /// <summary>
    /// Raises the event to each handler in the list.
    /// </summary>
    private void NotifyFileSystemEventArgs(
        WatcherChangeTypes changeType,
        ReadOnlySpan<char> name,
        FileExtendedInfo extendedInfo)
    {
        FileSystemExtendedEventHandler? handler = GetHandler(changeType);
        if (handler == null && _batchedEventArgs == null)
        {
            return;
        }

        if (MatchPattern(name.IsEmpty ? _directory : name))
        {
            handler?.Invoke(this, new FileSystemExtendedEventArgs(
                changeType,
                _directory,
                name.IsEmpty ? null : name.ToString(),
                extendedInfo));

            if (_batchedEventArgs != null)
            {
                BatchEvent(new RenamedExtendedEventArgs(
                    changeType,
                    _directory,
                    name.IsEmpty ? null : name.ToString(),
                    extendedInfo));
            }
        }
    }

    private void StartBatchingEvents(int maxNumberOfEvents)
    {
        _batchedEventArgs = _onNextEventsHandler != null ? new List<RenamedExtendedEventArgs>(maxNumberOfEvents) : null;
    }

    private void BatchEvent(RenamedExtendedEventArgs args)
    {
        _batchedEventArgs?.Add(args);
    }

    private void FinishBatchingEvents()
    {
        if (_batchedEventArgs?.Count > 0)
        {
            OnNextEvents(_batchedEventArgs.AsReadOnly());
        }

        _batchedEventArgs = null;
    }

    protected void OnChanged(FileSystemExtendedEventArgs e)
    {
        InvokeOn(e, _onChangedHandler);
    }

    protected void OnCreated(FileSystemExtendedEventArgs e)
    {
        InvokeOn(e, _onCreatedHandler);
    }

    protected void OnDeleted(FileSystemExtendedEventArgs e)
    {
        InvokeOn(e, _onDeletedHandler);
    }

    private void InvokeOn(FileSystemExtendedEventArgs e, FileSystemExtendedEventHandler? handler)
    {
        if (handler != null)
        {
            ISynchronizeInvoke? syncObj = SynchronizingObject;
            if (syncObj != null && syncObj.InvokeRequired)
                syncObj.BeginInvoke(handler, new object[] { this, e });
            else
                handler(this, e);
        }
    }

    /// <summary>
    ///    Raises the <see cref='FileSystemExtendedWatcher.Error'/> event.
    /// </summary>
    protected void OnError(ErrorExtendedEventArgs e)
    {
        ErrorExtendedEventHandler? handler = _onErrorHandler;
        if (handler != null)
        {
            ISynchronizeInvoke? syncObj = SynchronizingObject;
            if (syncObj != null && syncObj.InvokeRequired)
                syncObj.BeginInvoke(handler, new object[] { this, e });
            else
                handler(this, e);
        }
    }

    protected void OnRenamed(RenamedExtendedEventArgs e)
    {
        RenamedExtendedEventHandler? handler = _onRenamedHandler;
        if (handler != null)
        {
            ISynchronizeInvoke? syncObj = SynchronizingObject;
            if (syncObj != null && syncObj.InvokeRequired)
                syncObj.BeginInvoke(handler, new object[] { this, e });
            else
                handler(this, e);
        }
    }

    protected void OnNextEvents(IReadOnlyCollection<RenamedExtendedEventArgs> e)
    {
        FileSystemExtendedMultiEventHandler? handler = _onNextEventsHandler;
        if (handler != null)
        {
            ISynchronizeInvoke? syncObj = SynchronizingObject;
            if (syncObj != null && syncObj.InvokeRequired)
                syncObj.BeginInvoke(handler, new object[] { this, e });
            else
                handler(this, e);
        }
    }

    /// <summary>
    ///     Stops and starts this object.
    /// </summary>
    private void Restart()
    {
        if ((!IsSuspended()) && _enabled)
        {
            StopRaisingEvents();
            StartRaisingEventsIfNotDisposed();
        }
    }

    private void StartRaisingEventsIfNotDisposed()
    {
        //Cannot allocate the directoryHandle and the readBuffer if the object has been disposed; finalization has been suppressed.
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
        StartRaisingEvents();
    }

    public override ISite? Site
    {
        get
        {
            return base.Site;
        }
        set
        {
            base.Site = value;

            // set EnableRaisingEvents to true at design time so the user
            // doesn't have to manually.
            if (Site != null && Site.DesignMode)
                EnableRaisingEvents = true;
        }
    }

    public ISynchronizeInvoke? SynchronizingObject { get; set; }

    public void BeginInit()
    {
        bool oldEnabled = _enabled;
        StopRaisingEvents();
        _enabled = oldEnabled;
        _initializing = true;
    }

    public void EndInit()
    {
        _initializing = false;
        // Start listening to events if _enabled was set to true at some point.
        if (_directory.Length != 0 && _enabled)
            StartRaisingEvents();
    }

    private bool IsSuspended()
    {
        return _initializing || DesignMode;
    }

    private sealed class NormalizedFilterCollection : Collection<string>
    {
        internal NormalizedFilterCollection()
            : base(new ImmutableStringList())
        {
        }

        protected override void InsertItem(int index, string item)
        {
            base.InsertItem(index, string.IsNullOrEmpty(item) || item == "*.*" ? "*" : item);
        }

        protected override void SetItem(int index, string item)
        {
            base.SetItem(index, string.IsNullOrEmpty(item) || item == "*.*" ? "*" : item);
        }

        internal string[] GetFilters() => ((ImmutableStringList)Items).Items;

        /// <summary>
        /// List that maintains its underlying data in an immutable array, such that the list
        /// will never modify an array returned from its Items property. This is to allow
        /// the array to be enumerated safely while another thread might be concurrently mutating
        /// the collection.
        /// </summary>
        private sealed class ImmutableStringList : IList<string>
        {
            public string[] Items { get; private set; } = Array.Empty<string>();

            public string this[int index]
            {
                get
                {
                    string[] items = Items;
                    if ((uint)index >= (uint)items.Length)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return items[index];
                }
                set
                {
                    string[] clone = (string[])Items.Clone();
                    clone[index] = value;
                    Items = clone;
                }
            }

            public int Count => Items.Length;

            public bool IsReadOnly => false;

            public void Add(string item)
            {
                // Collection<T> doesn't use this method.
                throw new NotSupportedException();
            }

            public void Clear() => Items = Array.Empty<string>();

            public bool Contains(string item) => Array.IndexOf(Items, item) != -1;

            public void CopyTo(string[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);

            public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)Items).GetEnumerator();

            public int IndexOf(string item) => Array.IndexOf(Items, item);

            public void Insert(int index, string item)
            {
                string[] items = Items;
                string[] newItems = new string[items.Length + 1];
                items.AsSpan(0, index).CopyTo(newItems);
                items.AsSpan(index).CopyTo(newItems.AsSpan(index + 1));
                newItems[index] = item;
                Items = newItems;
            }

            public bool Remove(string item)
            {
                // Collection<T> doesn't use this method.
                throw new NotSupportedException();
            }

            public void RemoveAt(int index)
            {
                string[] items = Items;
                string[] newItems = new string[items.Length - 1];
                items.AsSpan(0, index).CopyTo(newItems);
                items.AsSpan(index + 1).CopyTo(newItems.AsSpan(index));
                Items = newItems;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtonDrive.Shared;

public readonly struct Disposable<T> : IDisposable
{
    private readonly IEnumerable<Action> _disposalActions;

    public Disposable(T value, IEnumerable<Action> disposalActions)
    {
        Value = value;
        _disposalActions = disposalActions;
    }

    public Disposable(T value, params Action[] disposalActions)
        : this(value, (IEnumerable<Action>)disposalActions)
    {
    }

    public Disposable(T value, IEnumerable<IDisposable> disposables)
        : this(value, disposables.Select(disposable => new Action(disposable.Dispose)))
    {
    }

    public Disposable(T value, params IDisposable[] disposables)
        : this(value, (IEnumerable<IDisposable>)disposables)
    {
    }

    public T Value { get; }

    public void Dispose()
    {
        foreach (var action in _disposalActions)
        {
            action.Invoke();
        }
    }
}

public static class Disposable
{
    public static Disposable<T> Create<T>(T value, params Action[] disposalActions)
    {
        return new Disposable<T>(value, disposalActions);
    }

    public static Disposable<T> Create<T>(T value, IEnumerable<IDisposable> disposables)
    {
        return new Disposable<T>(value, disposables.Select(disposable => new Action(disposable.Dispose)));
    }

    public static Disposable<T> Create<T>(T value, params IDisposable[] disposables)
    {
        return Create(value, (IEnumerable<IDisposable>)disposables);
    }
}

using System;
using System.Reactive.Disposables;
using ReactiveUI;

namespace Valeria.Src.Core.Mvvm;

/// <summary>
/// Base class for simple view models without complex state.
/// Provides disposal of reactive subscriptions.
/// </summary>
public abstract class ViewModelBase : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private bool _isDisposed;

    protected CompositeDisposable Disposables => _disposables;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
            _disposables.Dispose();

        _isDisposed = true;
    }
}

/// <summary>
/// Base class for view models of screens and complex components.
/// Holds a single immutable state record mutated only through <see cref="UpdateState"/>.
/// </summary>
/// <typeparam name="TState">Immutable state record type.</typeparam>
public abstract class ViewModelBase<TState> : ViewModelBase where TState : notnull
{
    private TState _state;

    protected ViewModelBase(TState initialState)
    {
        _state = initialState;
    }

    public TState State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    protected void UpdateState(Func<TState, TState> updater)
    {
        State = updater(State);
    }
}

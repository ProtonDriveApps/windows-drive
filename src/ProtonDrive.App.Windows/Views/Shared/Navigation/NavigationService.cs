using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Windows.Views.Shared.Navigation;

/// <inheritdoc cref="INavigationService{TPage}"/>
/// <inheritdoc cref="INavigatablePages{TPage}"/>
internal sealed class NavigationService<TPage> : ObservableObject, INavigationService<TPage>, INavigatablePages<TPage>
    where TPage : class, INavigatablePage
{
    private readonly SerialScheduler _scheduler = new();
    private readonly Stack<TrackedPage> _pages = new();

    private TPage? _currentPage;

    public NavigationService()
    {
        NavigateBackCommand = new RelayCommand(NavigateBack);
    }

    public TPage? CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value) && _currentPage != null)
            {
                _currentPage.OnActivated();
            }
        }
    }

    public ICommand NavigateBackCommand { get; }

    public void AddPage(TPage page)
    {
        page.Closed += PageOnClosed;
        Schedule(() => AddPage(new TrackedPage(page)));
    }

    private void NavigateBack()
    {
        CurrentPage?.Close();
    }

    private void PageOnClosed(object? sender, EventArgs e)
    {
        if (sender is not TPage page)
        {
            return;
        }

        Schedule(() => PageClosed(page));
    }

    private void AddPage(TrackedPage page)
    {
        _pages.Push(page);

        CurrentChanged();
    }

    private void PageClosed(TPage page)
    {
        page.Closed -= PageOnClosed;

        var trackedPage = _pages.FirstOrDefault(p => p.Page == page);
        if (trackedPage == null)
        {
            return;
        }

        trackedPage.Page = null;

        CurrentChanged();
    }

    private void CurrentChanged()
    {
        while (_pages.TryPeek(out var lastPage) && lastPage.Page == null)
        {
            _ = _pages.Pop();
        }

        CurrentPage = _pages.TryPeek(out var page) ? page.Page : null;
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }

    private class TrackedPage
    {
        public TrackedPage(TPage page)
        {
            Page = page;
        }

        public TPage? Page { get; set; }
    }
}

using System.ComponentModel;
using System.Windows.Input;

namespace ProtonDrive.App.Windows.Views.Shared.Navigation;

/// <summary>
/// Handles the list of navigatable pages and allows navigation between them.
/// Implements the <see cref="INotifyPropertyChanged"/> interface to be usable
/// for data binding.
/// </summary>
/// <typeparam name="TPage">The type of the page, should be a class implementing <see cref="INavigatablePage"/> interface.</typeparam>
public interface INavigationService<out TPage> : INotifyPropertyChanged
    where TPage : class, INavigatablePage
{
    /// <summary>
    /// The current page or null if there are no pages.
    /// </summary>
    TPage? CurrentPage { get; }

    /// <summary>
    /// Requests to navigate back to the previous page.
    /// </summary>
    /// <remarks>
    /// The navigation service first requests the current page to close. When the
    /// current page notifies it is closed by raising <see cref="INavigatablePage.Closed"/> event,
    /// the page is removed from the internal list of handled pages and the previous
    /// page becomes current. If there is no more pages, the <see cref="CurrentPage"/> is set to null.
    /// </remarks>
    ICommand NavigateBackCommand { get; }
}

using System;

namespace ProtonDrive.App.Windows.Views.Shared.Navigation;

/// <summary>
/// An interface through which navigation service interacts with pages it handles.
/// </summary>
public interface INavigatablePage
{
    /// <summary>
    /// Occurs when the page closes.
    /// </summary>
    /// <remarks>
    /// Notifies to the navigation service to remove the page from its list.
    /// </remarks>
    event EventHandler Closed;

    /// <summary>
    /// Informs the page become active.
    /// </summary>
    void OnActivated();

    /// <summary>
    /// Requests the page to close.
    /// </summary>
    /// <remarks>
    /// Navigation service requests the current page to close when the user requests
    /// to navigating back. The page can ignore the request, otherwise it should indicate
    /// the page closing by raising <see cref="Closed"/> event.
    /// </remarks>
    void Close();
}

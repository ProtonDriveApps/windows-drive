namespace ProtonDrive.App.Windows.Views.Shared.Navigation;

/// <summary>
/// A collection of navigatable pages handled by the navigation service.
/// </summary>
/// <typeparam name="TPage">The type of the page, should be a class implementing <see cref="INavigatablePage"/> interface.</typeparam>
public interface INavigatablePages<in TPage>
    where TPage : class, INavigatablePage
{
    /// <summary>
    /// Adds the page to the end of the list of navigatable pages and makes it current.
    /// </summary>
    /// <param name="page">The page to add to the list of navigatable pages.</param>
    void AddPage(TPage page);
}

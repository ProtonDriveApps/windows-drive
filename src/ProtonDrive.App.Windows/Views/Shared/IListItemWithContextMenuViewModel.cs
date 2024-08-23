namespace ProtonDrive.App.Windows.Views.Shared;

internal interface IListItemWithContextMenuViewModel
{
    public IOwnerItemCommands? ItemOnlyCommands { get; }
}

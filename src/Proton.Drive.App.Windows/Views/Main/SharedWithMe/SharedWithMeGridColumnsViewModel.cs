using Proton.Drive.App.Windows.Views.Shared.Sorting;

namespace Proton.Drive.App.Windows.Views.Main.SharedWithMe;

internal sealed class SharedWithMeGridColumnsViewModel
{
    private static readonly string PermissionsColumnTooltip = Resources.Strings.Main_SharedWithMe_Column_Permissions_Tooltip;

    public GridColumnViewModel PermissionsColumn { get; } = new(
        name: nameof(SharedWithMeItemViewModel.IsReadOnly),
        displayName: Resources.Strings.Main_SharedWithMe_Column_Permissions,
        tooltip: PermissionsColumnTooltip);

    public GridColumnViewModel NameColumn { get; } = new(
        name: nameof(SharedWithMeItemViewModel.Name),
        displayName: Resources.Strings.Main_SharedWithMe_Column_Name);

    public GridColumnViewModel SharedByColumn { get; } = new(
        name: nameof(SharedWithMeItemViewModel.InviterDisplayName),
        displayName: Resources.Strings.Main_SharedWithMe_Column_SharedBy);

    public GridColumnViewModel SharedOnColumn { get; } = new(
        name: nameof(SharedWithMeItemViewModel.SharingLocalDateTime),
        displayName: Resources.Strings.Main_SharedWithMe_Column_SharedOn);

    public GridColumnViewModel EnableSyncColumn { get; } = new(
        name: nameof(SharedWithMeItemViewModel.IsSyncEnabled),
        displayName: Resources.Strings.Main_SharedWithMe_Column_Sync);

    public void ClearSorting()
    {
        PermissionsColumn.ClearSorting();
        NameColumn.ClearSorting();
        SharedByColumn.ClearSorting();
        SharedOnColumn.ClearSorting();
        EnableSyncColumn.ClearSorting();
    }
}

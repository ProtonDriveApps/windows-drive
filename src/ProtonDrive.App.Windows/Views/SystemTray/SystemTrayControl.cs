using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit.Converters;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace ProtonDrive.App.Windows.Views.SystemTray;

internal sealed class SystemTrayControl : IDisposable
{
    private const string AppName = "Proton Drive";
    private const string CommaSeparator = ": ";

    private readonly SystemTrayViewModel _dataContext;

    private readonly Window _dummyOwnerWindow;
    private readonly NotifyIcon _wrappedControl = new();
    private readonly SystemTrayContextMenuView _contextMenu = new();
    private readonly AppIconStatusToIconConverter _iconConverter = new();
    private readonly EnumToDisplayTextConverter _enumConverter = EnumToDisplayTextConverter.Instance;

    public SystemTrayControl(SystemTrayViewModel dataContext)
    {
        _dataContext = dataContext;

        _dataContext.PropertyChanged += OnDataContextChanged;
        _wrappedControl.MouseClick += OnMouseClick;
        _wrappedControl.Text = AppName;

        _dummyOwnerWindow = new Window
        {
            Height = 0,
            Width = 0,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Visibility = Visibility.Collapsed,
        };

        PropertyChangedEventManager.AddHandler(
            ObservableSystemParameters.Instance,
            OnAppModeChanged,
            nameof(ObservableSystemParameters.SystemThemeColorMode));

        UpdateIcon();

        InitializeContextMenu();
    }

    public bool IsVisible
    {
        get => _wrappedControl.Visible;
        set => _wrappedControl.Visible = value;
    }

    public void Dispose()
    {
        _wrappedControl.Dispose();
    }

    private void OnAppModeChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateIcon();
    }

    private void OnDataContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemTrayViewModel.AppIconStatus))
        {
            UpdateIcon();
        }

        if (e.PropertyName == nameof(SystemTrayViewModel.AppDisplayStatus))
        {
            UpdateTooltip();
        }
    }

    private void UpdateTooltip()
    {
        _wrappedControl.Text = AppName + CommaSeparator + _enumConverter.Convert(_dataContext.AppDisplayStatus, null, null, null);
    }

    private void UpdateIcon()
    {
        _wrappedControl.Icon = _iconConverter.Convert(_dataContext.AppIconStatus, ObservableSystemParameters.Instance.SystemThemeColorMode);
    }

    private void InitializeContextMenu()
    {
        _contextMenu.DataContext = _dataContext;
        _contextMenu.StaysOpen = false;
        _dummyOwnerWindow.Show(); // Necessary to activate the window for displaying the context menu when required.
        _dummyOwnerWindow.Hide();
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        switch (e.Button)
        {
            case MouseButtons.Left:

                if (_dataContext.SignInCommand.CanExecute(null))
                {
                    _dataContext.SignInCommand.Execute(null);
                }
                else if (_dataContext.ShowMainWindowCommand.CanExecute(null))
                {
                    _dataContext.ShowMainWindowCommand.Execute(null);
                }

                _dummyOwnerWindow.Activate(); // Necessary to force the context menu to obtain the focus and work properly.
                break;

            case MouseButtons.Right:
                _contextMenu.Placement = PlacementMode.MousePoint;
                _contextMenu.IsOpen = true;
                _dummyOwnerWindow.Activate(); // Necessary to force the context menu to obtain the focus and work properly.
                break;
        }
    }
}

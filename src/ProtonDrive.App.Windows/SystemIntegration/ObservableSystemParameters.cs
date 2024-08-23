using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using PowerLineStatus = System.Windows.PowerLineStatus;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class ObservableSystemParameters : ObservableObject
{
    private static readonly Lazy<ObservableSystemParameters> LazyInstance = new(() => new ObservableSystemParameters());
    private static readonly double DpiX;

    private Thickness _resizeBorderThickness;
    private double _borderPaddingWidth;
    private bool _clientAreaAnimation;
    private PowerLineStatus _powerLineStatus;
    private ThemeColorMode _systemThemeColorMode;

    static ObservableSystemParameters()
    {
        SystemParameters.StaticPropertyChanged += OnSystemParametersPropertyChanged;

        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        DpiX = graphics.DpiX / 96d;
    }

    private ObservableSystemParameters()
    {
        UpdateAll();
    }

    public static ObservableSystemParameters Instance => LazyInstance.Value;

    public bool ClientAreaAnimation
    {
        get => _clientAreaAnimation;
        private set => SetProperty(ref _clientAreaAnimation, value);
    }

    public PowerLineStatus PowerLineStatus
    {
        get => _powerLineStatus;
        private set => SetProperty(ref _powerLineStatus, value);
    }

    public ThemeColorMode SystemThemeColorMode
    {
        get => _systemThemeColorMode;
        private set => SetProperty(ref _systemThemeColorMode, value);
    }

    public Thickness ResizeBorderThickness
    {
        get => _resizeBorderThickness;
        private set => SetProperty(ref _resizeBorderThickness, value);
    }

    public double BorderPaddingWidth
    {
        get => _borderPaddingWidth;
        private set => SetProperty(ref _borderPaddingWidth, value);
    }

    private static void OnSystemParametersPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (LazyInstance.IsValueCreated)
        {
            LazyInstance.Value.OnSystemParametersPropertyChanged(args.PropertyName);
        }
    }

    private void OnSystemParametersPropertyChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(SystemParameters.WindowResizeBorderThickness):
                UpdateResizeBorderThickness();
                UpdateBorderPaddingWidth();
                UpdateAppsUseLightTheme();
                break;

            case nameof(SystemParameters.ClientAreaAnimation):
                UpdateClientAreaAnimation();
                break;

            case nameof(SystemParameters.PowerLineStatus):
                UpdatePowerLineStatus();
                break;

            case null:
            case "":
                UpdateAll();
                break;

            default:
                // Nothing to do
                break;
        }
    }

    private void UpdateClientAreaAnimation()
    {
        ClientAreaAnimation = SystemParameters.ClientAreaAnimation;
    }

    private void UpdatePowerLineStatus()
    {
        PowerLineStatus = SystemParameters.PowerLineStatus;
    }

    private void UpdateAppsUseLightTheme()
    {
        SystemThemeColorMode = SystemThemeProvider.GetThemeColorMode();
    }

    private void UpdateResizeBorderThickness()
    {
        ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness;
    }

    private void UpdateBorderPaddingWidth()
    {
        BorderPaddingWidth = UxTheme.GetThemeSysSize(IntPtr.Zero, UxTheme.SM_CXPADDEDBORDER) / DpiX;
    }

    private void UpdateAll()
    {
        UpdateResizeBorderThickness();
        UpdateBorderPaddingWidth();
        UpdateClientAreaAnimation();
        UpdatePowerLineStatus();
        UpdateAppsUseLightTheme();
    }

    private static class UxTheme
    {
        // ReSharper disable InconsistentNaming
#pragma warning disable SA1310 // Field names should not contain underscore
        public const int SM_CXPADDEDBORDER = 92;
#pragma warning restore SA1310 // Field names should not contain underscore

        [DllImport("uxtheme.dll", ExactSpelling = true)]
        public static extern int GetThemeSysSize(IntPtr hTheme, int iSizeId);

        // ReSharper restore InconsistentNaming
    }

    private static class SystemThemeProvider
    {
        private const string Key = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        private const string RegistryValue = "SystemUsesLightTheme";

        public static ThemeColorMode GetThemeColorMode()
        {
            var registryKey = Registry.CurrentUser.OpenSubKey(Key, true);
            var value = registryKey?.GetValue(RegistryValue);
            return value is > 0 ? ThemeColorMode.Light : ThemeColorMode.Dark;
        }
    }
}

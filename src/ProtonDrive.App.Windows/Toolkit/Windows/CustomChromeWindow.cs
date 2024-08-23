using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using MoreLinq;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit.Converters;

namespace ProtonDrive.App.Windows.Toolkit.Windows;

/// <summary>
/// Derives <see cref="Window"/> to provide configuration for a custom WPF-based chrome.
/// </summary>
public class CustomChromeWindow : Window
{
    private static readonly DependencyPropertyKey ActualTitleBarHitTestableHeightPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ActualTitleBarHitTestableHeight), typeof(double), typeof(CustomChromeWindow), new PropertyMetadata(default(double)));

    private static readonly DependencyPropertyKey ActualOuterBorderThicknessPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ActualOuterBorderThickness), typeof(Thickness), typeof(CustomChromeWindow), new PropertyMetadata(default(Thickness)));

    private static readonly DependencyPropertyKey ActualResizeBorderThicknessPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ActualResizeBorderThickness), typeof(Thickness), typeof(CustomChromeWindow), new PropertyMetadata(default(Thickness)));

#pragma warning disable SA1202 // Elements should be ordered by access
    public static readonly DependencyProperty TitleBarHeightProperty = DependencyProperty.Register(
        nameof(TitleBarHeight), typeof(double), typeof(CustomChromeWindow), new PropertyMetadata(18d, OnPropertyAffectingActualValuesChanged));
#pragma warning restore SA1202 // Elements should be ordered by access

    public static readonly DependencyProperty ActualTitleBarHitTestableHeightProperty = ActualTitleBarHitTestableHeightPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ActiveOuterBorderBrushProperty = DependencyProperty.Register(
        nameof(ActiveOuterBorderBrush), typeof(Brush), typeof(CustomChromeWindow), new PropertyMetadata(SystemColors.ActiveBorderBrush));

    public static readonly DependencyProperty ActiveTitleBarBackgroundProperty = DependencyProperty.Register(
        nameof(ActiveTitleBarBackground), typeof(Brush), typeof(CustomChromeWindow), new PropertyMetadata(SystemColors.GradientActiveCaptionBrush));

    public static readonly DependencyProperty InactiveTitleBarBackgroundProperty = DependencyProperty.Register(
        nameof(InactiveTitleBarBackground), typeof(Brush), typeof(CustomChromeWindow), new PropertyMetadata(SystemColors.InactiveCaptionBrush));

    public static readonly DependencyProperty ActiveTitleBarForegroundProperty = DependencyProperty.Register(
        nameof(ActiveTitleBarForeground), typeof(Brush), typeof(CustomChromeWindow), new PropertyMetadata(SystemColors.ActiveCaptionTextBrush));

    public static readonly DependencyProperty InactiveTitleBarForegroundProperty = DependencyProperty.Register(
        nameof(InactiveTitleBarForeground), typeof(Brush), typeof(CustomChromeWindow), new PropertyMetadata(SystemColors.InactiveCaptionTextBrush));

    public static readonly DependencyProperty TitleFontSizeProperty = DependencyProperty.Register(
        nameof(TitleFontSize), typeof(double), typeof(CustomChromeWindow));

    public static readonly DependencyProperty TitleFontWeightProperty = DependencyProperty.Register(
        nameof(TitleFontWeight), typeof(FontWeight), typeof(CustomChromeWindow), new PropertyMetadata(FontWeights.Normal));

    public static readonly DependencyProperty TitleBarPaddingProperty = DependencyProperty.Register(
        nameof(TitleBarPadding), typeof(Thickness), typeof(CustomChromeWindow), new PropertyMetadata(default(Thickness)));

    public static readonly DependencyProperty TitleBarLeftPartProperty = DependencyProperty.Register(
        nameof(TitleBarLeftPart), typeof(object), typeof(CustomChromeWindow));

    public static readonly DependencyProperty TitleBarLeftPartTemplateProperty = DependencyProperty.Register(
        nameof(TitleBarLeftPartTemplate), typeof(DataTemplate), typeof(CustomChromeWindow));

    public static readonly DependencyProperty TitleBarRightPartProperty = DependencyProperty.Register(
        nameof(TitleBarRightPart), typeof(object), typeof(CustomChromeWindow));

    public static readonly DependencyProperty TitleBarRightPartTemplateProperty = DependencyProperty.Register(
        nameof(TitleBarRightPartTemplate), typeof(DataTemplate), typeof(CustomChromeWindow));

    public static readonly DependencyProperty TitleBarButtonWidthProperty = DependencyProperty.Register(
        nameof(TitleBarButtonWidth), typeof(double), typeof(CustomChromeWindow), new PropertyMetadata(SystemParameters.WindowCaptionButtonWidth));

    public static readonly DependencyProperty TitleBarButtonHeightProperty = DependencyProperty.Register(
        nameof(TitleBarButtonHeight), typeof(double), typeof(CustomChromeWindow), new PropertyMetadata(SystemParameters.WindowCaptionButtonHeight));

    public static readonly DependencyProperty HideDisabledTitleBarSystemButtonsProperty = DependencyProperty.Register(
        nameof(HideDisabledTitleBarSystemButtons), typeof(bool), typeof(CustomChromeWindow), new PropertyMetadata(default(bool)));

    public static readonly DependencyProperty MinimizeIsAlwaysDisabledProperty = DependencyProperty.Register(
        nameof(MinimizeIsAlwaysDisabled), typeof(bool), typeof(CustomChromeWindow), new PropertyMetadata(false, OnCannotEverMinimizeOrMaximizeChanged));

    public static readonly DependencyProperty MaximizeIsAlwaysDisabledProperty = DependencyProperty.Register(
        nameof(MaximizeIsAlwaysDisabled), typeof(bool), typeof(CustomChromeWindow), new PropertyMetadata(false));

    public static readonly DependencyProperty OuterResizeBorderThicknessProperty = DependencyProperty.Register(
        nameof(OuterResizeBorderThickness), typeof(Thickness), typeof(CustomChromeWindow), new PropertyMetadata(OnPropertyAffectingActualValuesChanged));

    public static readonly DependencyProperty InnerResizeBorderThicknessProperty = DependencyProperty.Register(
        nameof(InnerResizeBorderThickness), typeof(Thickness), typeof(CustomChromeWindow), new PropertyMetadata(default(Thickness), OnPropertyAffectingActualValuesChanged));

    public static readonly DependencyProperty OuterBorderPaddingProperty = DependencyProperty.Register(
        nameof(OuterBorderPadding), typeof(Thickness), typeof(CustomChromeWindow), new PropertyMetadata(OnPropertyAffectingActualValuesChanged));

    public static readonly DependencyProperty ActualOuterBorderThicknessProperty = ActualOuterBorderThicknessPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ActualResizeBorderThicknessProperty = ActualResizeBorderThicknessPropertyKey.DependencyProperty;

    public static readonly DependencyProperty InactiveOuterBorderBrushProperty = DependencyProperty.Register(
        nameof(InactiveOuterBorderBrush), typeof(Brush), typeof(CustomChromeWindow), new PropertyMetadata(default(Brush)));

    public static readonly DependencyProperty HasSystemMenuProperty = DependencyProperty.Register(
        nameof(HasSystemMenu), typeof(bool), typeof(CustomChromeWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty TitleIsVisibleProperty = DependencyProperty.Register(
        nameof(TitleIsVisible), typeof(bool), typeof(CustomChromeWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty WindowCommandButtonStyleProperty = DependencyProperty.Register(
        nameof(WindowCommandButtonStyle),
        typeof(Style),
        typeof(CustomChromeWindow),
        new PropertyMetadata(default(Style)));

    public static readonly DependencyProperty CloseButtonStyleProperty = DependencyProperty.Register(
        nameof(CloseButtonStyle),
        typeof(Style),
        typeof(CustomChromeWindow),
        new PropertyMetadata(default(Style)));

    private static readonly IReadOnlyDictionary<int, Func<CustomChromeWindow, bool>> PropertyGetterToWindowStyleFlagMap
        = new Dictionary<int, Func<CustomChromeWindow, bool>>
        {
            [Win32.WS_MINIMIZEBOX] = w => !w.MinimizeIsAlwaysDisabled && w.HasSystemMenu && w.ResizeMode != ResizeMode.NoResize,
            [Win32.WS_MAXIMIZEBOX] = w =>
                !w.MaximizeIsAlwaysDisabled && w.HasSystemMenu && (w.ResizeMode == ResizeMode.CanResize || w.ResizeMode == ResizeMode.CanResizeWithGrip),
            [Win32.WS_SYSMENU] = w => w.HasSystemMenu,
        };

    static CustomChromeWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomChromeWindow), new FrameworkPropertyMetadata(typeof(CustomChromeWindow)));
        ResizeModeProperty.OverrideMetadata(typeof(CustomChromeWindow), new FrameworkPropertyMetadata(OnPropertyAffectingActualValuesChanged));
        WindowChrome.WindowChromeProperty.OverrideMetadata(typeof(CustomChromeWindow), new PropertyMetadata(null, null, CoerceWindowChrome));
    }

    public CustomChromeWindow()
    {
        FixSystemMenuAlwaysShowingOnTitleBarRightClick.Enable(this);
        FixNonTransparentWindowRenderingOnStateChange.Enable(this);

        // Those routed commands have no effect and are disabled unless we handle them.
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, CloseWindow, CanShowSystemMenu));
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, MinimizeWindow, CanMinimizeWindow));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, MaximizeWindow, CanMaximizeWindow));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, RestoreWindow, CanRestoreWindow));
        CommandBindings.Add(new CommandBinding(SystemCommands.ShowSystemMenuCommand, ShowSystemMenu, CanShowSystemMenu));
    }

    public double TitleBarHeight
    {
        get => (double)GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    public double ActualTitleBarHitTestableHeight
    {
        get => (double)GetValue(ActualTitleBarHitTestableHeightProperty);
        private set => SetValue(ActualTitleBarHitTestableHeightPropertyKey, value);
    }

    public Brush ActiveOuterBorderBrush
    {
        get => (Brush)GetValue(ActiveOuterBorderBrushProperty);
        set => SetValue(ActiveOuterBorderBrushProperty, value);
    }

    public Brush ActiveTitleBarBackground
    {
        get => (Brush)GetValue(ActiveTitleBarBackgroundProperty);
        set => SetValue(ActiveTitleBarBackgroundProperty, value);
    }

    public Brush InactiveTitleBarBackground
    {
        get => (Brush)GetValue(InactiveTitleBarBackgroundProperty);
        set => SetValue(InactiveTitleBarBackgroundProperty, value);
    }

    public Brush ActiveTitleBarForeground
    {
        get => (Brush)GetValue(ActiveTitleBarForegroundProperty);
        set => SetValue(ActiveTitleBarForegroundProperty, value);
    }

    public Brush InactiveTitleBarForeground
    {
        get => (Brush)GetValue(InactiveTitleBarForegroundProperty);
        set => SetValue(InactiveTitleBarForegroundProperty, value);
    }

    public double TitleFontSize
    {
        get => (double)GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    public FontWeight TitleFontWeight
    {
        get => (FontWeight)GetValue(TitleFontWeightProperty);
        set => SetValue(TitleFontWeightProperty, value);
    }

    public Thickness TitleBarPadding
    {
        get => (Thickness)GetValue(TitleBarPaddingProperty);
        set => SetValue(TitleBarPaddingProperty, value);
    }

    public object TitleBarLeftPart
    {
        get => GetValue(TitleBarLeftPartProperty);
        set => SetValue(TitleBarLeftPartProperty, value);
    }

    public DataTemplate TitleBarLeftPartTemplate
    {
        get => (DataTemplate)GetValue(TitleBarLeftPartTemplateProperty);
        set => SetValue(TitleBarLeftPartTemplateProperty, value);
    }

    public object TitleBarRightPart
    {
        get => GetValue(TitleBarRightPartProperty);
        set => SetValue(TitleBarRightPartProperty, value);
    }

    public DataTemplate TitleBarRightPartTemplate
    {
        get => (DataTemplate)GetValue(TitleBarRightPartTemplateProperty);
        set => SetValue(TitleBarRightPartTemplateProperty, value);
    }

    public double TitleBarButtonWidth
    {
        get => (double)GetValue(TitleBarButtonWidthProperty);
        set => SetValue(TitleBarButtonWidthProperty, value);
    }

    public double TitleBarButtonHeight
    {
        get => (double)GetValue(TitleBarButtonHeightProperty);
        set => SetValue(TitleBarButtonHeightProperty, value);
    }

    public bool HideDisabledTitleBarSystemButtons
    {
        get => (bool)GetValue(HideDisabledTitleBarSystemButtonsProperty);
        set => SetValue(HideDisabledTitleBarSystemButtonsProperty, value);
    }

    public bool MinimizeIsAlwaysDisabled
    {
        get => (bool)GetValue(MinimizeIsAlwaysDisabledProperty);
        set => SetValue(MinimizeIsAlwaysDisabledProperty, value);
    }

    public bool MaximizeIsAlwaysDisabled
    {
        get => (bool)GetValue(MaximizeIsAlwaysDisabledProperty);
        set => SetValue(MaximizeIsAlwaysDisabledProperty, value);
    }

    public Thickness OuterResizeBorderThickness
    {
        get => (Thickness)GetValue(OuterResizeBorderThicknessProperty);
        set => SetValue(OuterResizeBorderThicknessProperty, value);
    }

    public Thickness InnerResizeBorderThickness
    {
        get => (Thickness)GetValue(InnerResizeBorderThicknessProperty);
        set => SetValue(InnerResizeBorderThicknessProperty, value);
    }

    public Thickness OuterBorderPadding
    {
        get => (Thickness)GetValue(OuterBorderPaddingProperty);
        set => SetValue(OuterBorderPaddingProperty, value);
    }

    public Thickness ActualOuterBorderThickness
    {
        get => (Thickness)GetValue(ActualOuterBorderThicknessProperty);
        private set => SetValue(ActualOuterBorderThicknessPropertyKey, value);
    }

    public Thickness ActualResizeBorderThickness
    {
        get => (Thickness)GetValue(ActualResizeBorderThicknessProperty);
        private set => SetValue(ActualResizeBorderThicknessPropertyKey, value);
    }

    public Brush InactiveOuterBorderBrush
    {
        get => (Brush)GetValue(InactiveOuterBorderBrushProperty);
        set => SetValue(InactiveOuterBorderBrushProperty, value);
    }

    public bool HasSystemMenu
    {
        get => (bool)GetValue(HasSystemMenuProperty);
        set => SetValue(HasSystemMenuProperty, value);
    }

    public bool TitleIsVisible
    {
        get => (bool)GetValue(TitleIsVisibleProperty);
        set => SetValue(TitleIsVisibleProperty, value);
    }

    public Style WindowCommandButtonStyle
    {
        get => (Style)GetValue(WindowCommandButtonStyleProperty);
        set => SetValue(WindowCommandButtonStyleProperty, value);
    }

    public Style CloseButtonStyle
    {
        get => (Style)GetValue(CloseButtonStyleProperty);
        set => SetValue(CloseButtonStyleProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsLoaded)
        {
            // This is the earliest time when the native window handle is available to apply styles.
            ApplyNativeWindowStyle();

            // This is an opportunity to apply the window chrome without triggering a second measurement pass.
            InvalidateProperty(WindowChrome.WindowChromeProperty);
        }

        return base.MeasureOverride(availableSize);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        UpdateActualMeasurements();

        base.OnSourceInitialized(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        UpdateActualMeasurements();

        CommandManager.InvalidateRequerySuggested();

        base.OnStateChanged(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (e.Cancel || Owner == null)
        {
            return;
        }

        var lastSibling = Owner.OwnedWindows.Cast<Window>().LastOrDefault(window => !ReferenceEquals(window, this));
        var windowToActivate = lastSibling != null
            ? MoreEnumerable.TraverseDepthFirst(lastSibling, window => window.OwnedWindows.Cast<Window>()).Last()
            : Owner;
        windowToActivate.Activate();
    }

    private static void OnCannotEverMinimizeOrMaximizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CustomChromeWindow)d).ApplyNativeWindowStyle();
    }

    private static void OnPropertyAffectingActualValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CustomChromeWindow)d).UpdateActualMeasurements();
    }

    private static object CoerceWindowChrome(DependencyObject dependencyObject, object? baseValue)
    {
        var window = (CustomChromeWindow)dependencyObject;
        var result = window.CreateWindowChrome();
        if (baseValue != null)
        {
            var baseChrome = (WindowChrome)baseValue;
            result.CornerRadius = baseChrome.CornerRadius;
        }

        window.InvalidateMeasure();
        return result;
    }

    private static WindowChrome CreateDefaultWindowChrome()
    {
        return new()
        {
            CaptionHeight = SystemParameters.WindowCaptionHeight,
            ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
            CornerRadius = default,
            NonClientFrameEdges = NonClientFrameEdges.None,
            UseAeroCaptionButtons = false,
        };
    }

    private static bool HasNativeStyles(IntPtr windowHandle, int flags)
    {
        if (IntPtr.Size == 8)
        {
            var style = Win32.GetWindowLongPtr(windowHandle, Win32.GWL_STYLE);
            return (style & flags) == flags;
        }

        {
            var style = Win32.GetWindowLong(windowHandle, Win32.GWL_STYLE);
            return (style & flags) == flags;
        }
    }

    private static void AddAndRemoveNativeWindowStyles(IntPtr windowHandle, long addFlags, long removeFlags)
    {
        if (IntPtr.Size == 8)
        {
            var previousStyle = Win32.GetWindowLongPtr(windowHandle, Win32.GWL_STYLE);
            var newStyle = previousStyle & ~removeFlags;
            newStyle |= addFlags;
            Win32.SetWindowLongPtr(windowHandle, Win32.GWL_STYLE, newStyle);
        }
        else
        {
            var previousStyle = Win32.GetWindowLong(windowHandle, Win32.GWL_STYLE);
            var newStyle = previousStyle & ~removeFlags;
            newStyle |= addFlags;
            Win32.SetWindowLong(windowHandle, Win32.GWL_STYLE, newStyle);
        }
    }

    private void CanMinimizeWindow(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = WindowState != WindowState.Minimized
                       && ResizeMode != ResizeMode.NoResize && !MinimizeIsAlwaysDisabled;
    }

    private void CanMaximizeWindow(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = WindowState != WindowState.Maximized
                       && (ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip)
                       && !MaximizeIsAlwaysDisabled;
    }

    private void CanRestoreWindow(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = WindowState != WindowState.Normal
                       && (ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip)
                       && !MaximizeIsAlwaysDisabled;
    }

    private void CanShowSystemMenu(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = HasSystemMenu;
    }

    private void MinimizeWindow(object target, ExecutedRoutedEventArgs e)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void MaximizeWindow(object target, ExecutedRoutedEventArgs e)
    {
        SystemCommands.MaximizeWindow(this);
    }

    private void RestoreWindow(object target, ExecutedRoutedEventArgs e)
    {
        SystemCommands.RestoreWindow(this);
    }

    private void CloseWindow(object target, ExecutedRoutedEventArgs e)
    {
        SystemCommands.CloseWindow(this);
    }

    private void ShowSystemMenu(object target, ExecutedRoutedEventArgs e)
    {
        var actualOuterBorderThickness = ActualOuterBorderThickness;
        var coordinatesUnderTitleBar = new Point(
            actualOuterBorderThickness.Left,
            actualOuterBorderThickness.Top + TitleBarHeight);

        var coordinates = PointToScreen(coordinatesUnderTitleBar);

        SystemCommands.ShowSystemMenu(this, new LogicalPointToDevicePointConverter(this).ConvertBack(coordinates));
    }

    private void ApplyNativeWindowStyle()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var removeFlags = 0;
        var addFlags = 0;

        foreach (var entry in PropertyGetterToWindowStyleFlagMap)
        {
            var styleShouldBeActivated = entry.Value(this);
            if (styleShouldBeActivated)
            {
                addFlags |= entry.Key;
            }
            else
            {
                removeFlags |= entry.Key;
            }
        }

        // This has to be done in two steps to avoid artifacts from caption redraw.
        AddAndRemoveNativeWindowStyles(windowHandle, addFlags, removeFlags | Win32.WS_CAPTION);

        Win32.SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            Win32.SWP_FRAMECHANGED | Win32.SWP_NOSIZE | Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOOWNERZORDER | Win32.SWP_NOACTIVATE);

        AddAndRemoveNativeWindowStyles(windowHandle, Win32.WS_CAPTION, 0);
    }

    private void UpdateActualMeasurements()
    {
        if (WindowState == WindowState.Maximized)
        {
            var borderPaddingWidth = ObservableSystemParameters.Instance.BorderPaddingWidth;
            var resizeBorderThickness = ObservableSystemParameters.Instance.ResizeBorderThickness;

            var resizableWindowTotalBorderThickness = new Thickness(
                resizeBorderThickness.Left + borderPaddingWidth,
                resizeBorderThickness.Top + borderPaddingWidth,
                resizeBorderThickness.Right + borderPaddingWidth,
                resizeBorderThickness.Bottom + borderPaddingWidth);

            ActualOuterBorderThickness = resizableWindowTotalBorderThickness;
            ActualResizeBorderThickness = resizableWindowTotalBorderThickness;
            ActualTitleBarHitTestableHeight = TitleBarHeight;
        }
        else
        {
            var outerResizeBorderThickness = OuterResizeBorderThickness;
            var innerResizeBorderThickness = InnerResizeBorderThickness;
            var windowBorderPadding = OuterBorderPadding;

            var actualOuterWindowBorderThickness = new Thickness(
                outerResizeBorderThickness.Left + windowBorderPadding.Left,
                outerResizeBorderThickness.Top + windowBorderPadding.Top,
                outerResizeBorderThickness.Right + windowBorderPadding.Right,
                outerResizeBorderThickness.Bottom + windowBorderPadding.Bottom);

            ActualOuterBorderThickness = actualOuterWindowBorderThickness;

            ActualResizeBorderThickness = ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip
                ? new Thickness(
                    actualOuterWindowBorderThickness.Left + innerResizeBorderThickness.Left,
                    actualOuterWindowBorderThickness.Top + innerResizeBorderThickness.Top,
                    actualOuterWindowBorderThickness.Right + innerResizeBorderThickness.Right,
                    actualOuterWindowBorderThickness.Bottom + innerResizeBorderThickness.Bottom)
                : actualOuterWindowBorderThickness;

            ActualTitleBarHitTestableHeight = Math.Max(TitleBarHeight - innerResizeBorderThickness.Top, 0);
        }
    }

    private WindowChrome CreateWindowChrome()
    {
        var result = CreateDefaultWindowChrome();

        BindingOperations.SetBinding(
            result,
            WindowChrome.CaptionHeightProperty,
            new Binding
            {
                Path = new PropertyPath(ActualTitleBarHitTestableHeightProperty),
                Source = this,
            });

        BindingOperations.SetBinding(
            result,
            WindowChrome.ResizeBorderThicknessProperty,
            new Binding
            {
                Path = new PropertyPath(ActualResizeBorderThicknessProperty),
                Source = this,
            });

        return result;
    }

    /// <summary>
    /// Works around WindowChrome still displaying the system menu even though the WS_SYSMENU style is not set.
    /// This is done by muting the right button up message.
    /// </summary>
    private static class FixSystemMenuAlwaysShowingOnTitleBarRightClick
    {
#pragma warning disable SA1310 // Field names should not contain underscore

        // ReSharper disable once InconsistentNaming
        private const int WM_NCRBUTTONUP = 0xa5;

#pragma warning restore SA1310 // Field names should not contain underscore

        public static void Enable(Window window)
        {
            if (window.IsInitialized)
            {
                StartListening(window);
            }
            else
            {
                window.SourceInitialized += (_, _) => StartListening(window);
            }
        }

        private static void StartListening(Window window)
        {
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            hwndSource?.AddHook((IntPtr hWnd, int msg, IntPtr _, IntPtr _, ref bool handled) =>
            {
                if (msg == WM_NCRBUTTONUP && !HasNativeStyles(hWnd, Win32.WS_SYSMENU))
                {
                    handled = true;
                }

                return IntPtr.Zero;
            });
        }
    }

    /// <summary>
    /// Fixes a rendering bug (noticed on classic theme) when changing states without transparency activated.
    /// </summary>
    private static class FixNonTransparentWindowRenderingOnStateChange
    {
        public static void Enable(Window window)
        {
            window.ContentRendered += OnStateChanged;
            window.StateChanged += OnStateChanged;
        }

        private static void OnStateChanged(object? sender, EventArgs eventArgs)
        {
            if (sender is not Window window)
            {
                return;
            }

            if (!window.AllowsTransparency)
            {
                window.Dispatcher.InvokeAsync(
                    () => Win32.RedrawWindow(new WindowInteropHelper(window).Handle, IntPtr.Zero, IntPtr.Zero, Win32.RDW_INVALIDATE),
                    DispatcherPriority.ApplicationIdle);
            }
        }
    }

    private static class Win32
    {
        // ReSharper disable InconsistentNaming
#pragma warning disable SA1310 // Field names should not contain underscore

        public const int GWL_STYLE = -16;

        public const int WS_MAXIMIZEBOX = 0x10000;
        public const int WS_MINIMIZEBOX = 0x20000;
        public const int WS_SYSMENU = 0x80000;
        public const int WS_CAPTION = 0xc00000;

        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOOWNERZORDER = 0x0200;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;

        public const uint RDW_INVALIDATE = 0x1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

#pragma warning restore SA1310 // Field names should not contain underscore

        // ReSharper restore InconsistentNaming
    }
}

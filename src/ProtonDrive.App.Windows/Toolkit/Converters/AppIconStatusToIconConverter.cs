using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Views.Shared;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

internal sealed class AppIconStatusToIconConverter
{
    private readonly Dictionary<(AppIconStatus, ThemeColorMode), Icon> _icons = new();

    public Icon? Convert(AppIconStatus appIconStatus, ThemeColorMode themeColorMode)
    {
        if (!TryGetIcon(appIconStatus, themeColorMode, out var icon))
        {
            if (themeColorMode != ThemeColorMode.Dark || !TryGetIcon(appIconStatus, ThemeColorMode.Light, out icon))
            {
                return null;
            }

            _icons.Add((appIconStatus, themeColorMode), icon);
        }

        return icon;
    }

    private bool TryGetIcon(AppIconStatus appIconStatus, ThemeColorMode themeColorMode, [MaybeNullWhen(false)] out Icon icon)
    {
        if (!_icons.TryGetValue((appIconStatus, themeColorMode), out icon))
        {
            var iconPath = $"Resources.Icons.Notification.{Enum.GetName(typeof(AppIconStatus), appIconStatus)}"
                           + $"{(themeColorMode == ThemeColorMode.Dark ? ".Dark" : string.Empty)}.ico";

            if (!IconResource.TryCreate(iconPath, out var iconResource))
            {
                icon = null;
                return false;
            }

            icon = iconResource.GetIcon();

            _icons.Add((appIconStatus, themeColorMode), icon);
        }

        return true;
    }
}

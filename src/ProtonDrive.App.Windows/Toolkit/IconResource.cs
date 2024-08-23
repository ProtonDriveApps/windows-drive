using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ProtonDrive.App.Windows.Toolkit;

internal sealed class IconResource
{
    private static readonly Assembly Assembly = typeof(IconResource).Assembly;
    private static readonly string BasePath = typeof(App).Namespace + ".";
    private static readonly HashSet<string> ResourceNames = Assembly.GetManifestResourceNames().ToHashSet();

    private readonly string _iconPath;

    private Icon? _cachedIcon;

    public static bool TryCreate(string iconPath, [MaybeNullWhen(false)] out IconResource iconResource)
    {
        var fullIconPath = BasePath + iconPath;
        if (!ResourceNames.Contains(fullIconPath))
        {
            iconResource = null;
            return false;
        }

        iconResource = new IconResource(fullIconPath);
        return true;
    }

    private IconResource(string iconPath)
    {
        _iconPath = iconPath;
    }

    public Icon GetIcon()
    {
        return _cachedIcon ??= new Icon(GetIconStream());
    }

    private Stream GetIconStream()
    {
        var stream = Assembly.GetManifestResourceStream(_iconPath);

        if (stream == null)
        {
            throw new InvalidOperationException($"No resource found at path \"{_iconPath}\"");
        }

        return stream;
    }
}

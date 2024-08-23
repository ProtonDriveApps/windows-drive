using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ProtonDrive.App.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Win32 naming convention")]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming convention")]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Win32 naming convention")]
internal static class Comctl32
{
    [DllImport(Libraries.ComCtl32, SetLastError = false, ExactSpelling = true)]
    public static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, IMAGELISTDRAWFLAGS flags);

    [Flags]
    public enum IMAGELISTDRAWFLAGS : uint
    {
        /// <summary>
        /// Draws the image using the background color for the image list. If the background color is the CLR_NONE value, the image is
        /// drawn transparently using the mask.
        /// </summary>
        ILD_NORMAL = 0X00000000,

        /// <summary>
        /// Draws the image transparently using the mask, regardless of the background color. This value has no effect if the image list
        /// does not contain a mask.
        /// </summary>
        ILD_TRANSPARENT = 0x00000001,

        /// <summary>
        /// Draws the image, blending 25 percent with the blend color specified by rgbFg. This value has no effect if the image list does
        /// not contain a mask.
        /// </summary>
        ILD_BLEND25 = 0X00000002,

        /// <summary>Same as ILD_BLEND25</summary>
        ILD_FOCUS = ILD_BLEND25,

        /// <summary>
        /// Draws the image, blending 50 percent with the blend color specified by rgbFg. This value has no effect if the image list does
        /// not contain a mask.
        /// </summary>
        ILD_BLEND50 = 0X00000004,

        /// <summary>Same as ILD_BLEND50</summary>
        ILD_SELECTED = ILD_BLEND50,

        /// <summary>Same as ILD_BLEND50</summary>
        ILD_BLEND = ILD_BLEND50,

        /// <summary>Draws the mask.</summary>
        ILD_MASK = 0X00000010,

        /// <summary>If the overlay does not require a mask to be drawn, set this flag.</summary>
        ILD_IMAGE = 0X00000020,

        /// <summary>Draws the image using the raster operation code specified by the dwRop member.</summary>
        ILD_ROP = 0X00000040,

        /// <summary>
        /// To extract the overlay image from the fStyle member, use the logical AND to combine fStyle with the ILD_OVERLAYMASK value.
        /// </summary>
        ILD_OVERLAYMASK = 0x00000F00,

        /// <summary>Preserves the alpha channel in the destination.</summary>
        ILD_PRESERVEALPHA = 0x00001000,

        /// <summary>Causes the image to be scaled to cx, cy instead of being clipped.</summary>
        ILD_SCALE = 0X00002000,

        /// <summary>Scales the image to the current dpi of the display.</summary>
        ILD_DPISCALE = 0X00004000,

        /// <summary>
        /// <c>Windows Vista and later.</c> Draw the image if it is available in the cache. Do not extract it automatically. The called
        /// draw method returns E_PENDING to the calling component, which should then take an alternative action, such as, provide
        /// another image and queue a background task to force the image to be loaded via ForceImagePresent using the ILFIP_ALWAYS flag.
        /// The ILD_ASYNC flag then prevents the extraction operation from blocking the current thread and is especially important if a
        /// draw method is called from the user interface (UI) thread.
        /// </summary>
        ILD_ASYNC = 0X00008000
    }
}

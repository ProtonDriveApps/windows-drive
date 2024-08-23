using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using ProtonDrive.App.Windows.Services;

namespace ProtonDrive.App.Windows.Interop;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Win32 naming convention")]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming convention")]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Win32 naming convention")]
internal static class Shell32
{
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo

    /// <summary>Maximal Length of unmanaged Windows-Path-strings</summary>
    private const int MAX_PATH = 260;

    /// <summary>Maximal Length of unmanaged Typename</summary>
    private const int MAX_TYPE = 80;

    [Flags]
    public enum SHGFI : int
    {
        /// <summary>get icon</summary>
        Icon = 0x000000100,

        /// <summary>get display name</summary>
        DisplayName = 0x000000200,

        /// <summary>get type name</summary>
        TypeName = 0x000000400,

        /// <summary>get attributes</summary>
        Attributes = 0x000000800,

        /// <summary>get icon location</summary>
        IconLocation = 0x000001000,

        /// <summary>return exe type</summary>
        ExeType = 0x000002000,

        /// <summary>get system icon index</summary>
        SysIconIndex = 0x000004000,

        /// <summary>put a link overlay on icon</summary>
        LinkOverlay = 0x000008000,

        /// <summary>show icon in selected state</summary>
        Selected = 0x000010000,

        /// <summary>get only specified attributes</summary>
        Attr_Specified = 0x000020000,

        /// <summary>get large icon</summary>
        LargeIcon = 0x000000000,

        /// <summary>get small icon</summary>
        SmallIcon = 0x000000001,

        /// <summary>get open icon</summary>
        OpenIcon = 0x000000002,

        /// <summary>get shell size icon</summary>
        ShellIconSize = 0x000000004,

        /// <summary>pszPath is a pidl</summary>
        PIDL = 0x000000008,

        /// <summary>use passed dwFileAttribute</summary>
        UseFileAttributes = 0x000000010,

        /// <summary>apply the appropriate overlays</summary>
        AddOverlays = 0x000000020,

        /// <summary>Get the index of the overlay in the upper 8 bits of the iIcon</summary>
        OverlayIndex = 0x000000040,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEINFOW
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_TYPE)]
        public string szTypeName;

        public SHFILEINFOW(bool b)
        {
            hIcon = IntPtr.Zero;
            iIcon = 0;
            dwAttributes = 0;
            szDisplayName = string.Empty;
            szTypeName = string.Empty;
        }
    }

    [DllImport(Libraries.Shell32, CharSet = CharSet.Auto, SetLastError = false)]
    public static extern IntPtr ILCreateFromPath(string pszPath);

    [DllImport(Libraries.Shell32, ExactSpelling = true, SetLastError = false)]
    public static extern void ILFree(IntPtr pidl);

    [DllImport(Libraries.Shell32, CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfoW(
        string pszPath,
        FileAttributes dwFileAttributes,
        ref SHFILEINFOW psfi,
        uint cbfileInfo,
        SHGFI uFlags);

    [DllImport(Libraries.Shell32, CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfoW(
        IntPtr pidl,
        FileAttributes dwFileAttributes,
        ref SHFILEINFOW psfi,
        uint cbfileInfo,
        SHGFI uFlags);

    [DllImport(Libraries.Shell32)]
    public static extern int SHGetKnownFolderIDList(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppidl);

    [DllImport(Libraries.Shell32, CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
    public static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, nint hToken = 0);

    [DllImport(Libraries.Shell32, SetLastError = true)]
    public static extern HResult SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    [DllImport(Libraries.Shell32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern HResult SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object item);

    public static readonly Guid IID_IShellItem = new(InterfaceGuidStrings.IShellItem);
    private static readonly Guid CLSID_LocalThumbnailCache = new(ClassGuidStrings.LocalThumbnailCache);

    [ComImport]
    [Guid(InterfaceGuidStrings.IShellItem)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IntPtr ppv);

        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid(InterfaceGuidStrings.IThumbnailCache)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IThumbnailCache
    {
        [PreserveSig]
        HResult GetThumbnail(
            [In] IShellItem pShellItem,
            [In] uint cxyRequestedThumbSize,
            [In] WTS_FLAGS flags,
            [Out][MarshalAs(UnmanagedType.Interface)] out ISharedBitmap ppvThumb,
            [Out] out WTS_CACHEFLAGS pOutFlags,
            [Out] out WTS_THUMBNAILID pThumbnailID);

        [PreserveSig]
        HResult GetThumbnailByID(
            [In, MarshalAs(UnmanagedType.Struct)] WTS_THUMBNAILID thumbnailID,
            [In] uint cxyRequestedThumbSize,
            [Out][MarshalAs(UnmanagedType.Interface)] out ISharedBitmap ppvThumb,
            [Out] WTS_CACHEFLAGS pOutFlags);
    }

    [ComImport]
    [Guid(InterfaceGuidStrings.ISharedBitmap)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISharedBitmap
    {
        HResult GetSharedBitmap([Out] out IntPtr phbm);
        HResult GetSize([Out] out Size pSize);
        HResult GetFormat([Out] out WTS_ALPHATYPE pat);
        HResult InitializeBitmap([In] IntPtr hbm, [In] WTS_ALPHATYPE wtsAT);
        HResult Detach([Out] out IntPtr phbm);
    }

    public enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVE = 0x80080001,
        SIGDN_PARENTRELATIVEFORUI = 0x80094001,
    }

    [Flags]
    public enum SIIGBF : uint
    {
        DEFAULT = 0x00,
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10,
    }

    [Flags]
    public enum WTS_FLAGS : uint
    {
        WTS_NONE = 0x00000000,
        WTS_EXTRACT = 0x00000000,
        WTS_INCACHEONLY = 0x00000001,
        WTS_FASTEXTRACT = 0x00000002,
        WTS_FORCEEXTRACTION = 0x00000004,
        WTS_SLOWRECLAIM = 0x00000008,
        WTS_EXTRACTDONOTCACHE = 0x00000020,
        WTS_SCALETOREQUESTEDSIZE = 0x00000040,
        WTS_SKIPFASTEXTRACT = 0x00000080,
        WTS_EXTRACTINPROC = 0x00000100,
        WTS_CROPTOSQUARE = 0x00000200,
        WTS_INSTANCESURROGATE = 0x00000400,
        WTS_REQUIRESURROGATE = 0x00000800,
        WTS_APPSTYLE = 0x00002000,
        WTS_WIDETHUMBNAILS = 0x00004000,
        WTS_IDEALCACHESIZEONLY = 0x00008000,
        WTS_SCALEUP = 0x00010000,
    }

    public enum WTS_ALPHATYPE
    {
        WTSAT_RGB = 1,
        WTSAT_ARGB = 2,
    }

    [Flags]
    public enum WTS_CACHEFLAGS : uint
    {
        WTS_DEFAULT = 0x00000000,
        WTS_LOWQUALITY = 0x00000001,
        WTS_CACHED = 0x00000002,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WTS_THUMBNAILID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        private readonly byte[] _rgbKey;
    }

    private static class InterfaceGuidStrings
    {
        public const string IShellItem = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
        public const string IThumbnailCache = "F676C15D-596A-4CE2-8234-33996F445DB1";
        public const string ISharedBitmap = "091162A4-BC96-411F-AAE8-C5122CD03363";
    }

    private static class ClassGuidStrings
    {
        public const string LocalThumbnailCache = "50EF4544-AC9F-4A8E-B21B-8A26180DB13F";
    }

    public static class ThumbnailCache
    {
        private static readonly Type? Type = Type.GetTypeFromCLSID(CLSID_LocalThumbnailCache);

        public static IThumbnailCache GetInstance()
        {
            if (Type is null)
            {
                throw new ThumbnailGenerationException("Could not get type of local thumbnail cache");
            }

            return (IThumbnailCache)(Activator.CreateInstance(Type)
                                     ?? throw new ThumbnailGenerationException("Could not get instance of local thumbnail cache"));
        }
    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore IdentifierTypo
}

// dllmain.h : Declaration of module class.

class CWindowsShellExtensionModule : public ATL::CAtlDllModuleT< CWindowsShellExtensionModule >
{
public:
    DECLARE_LIBID(LIBID_WindowsShellExtensionLib)
    DECLARE_REGISTRY_APPID_RESOURCEID(IDR_WINDOWSSHELLEXTENSION, "{e7c15560-a668-4cc7-b801-63016cf7aeec}")
};

extern class CWindowsShellExtensionModule _AtlModule;

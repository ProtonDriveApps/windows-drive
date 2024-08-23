#pragma once

#include "resource.h"
#include "pch.h"

#include "WindowsShellExtension_i.h"

#include "MoveToDriveCommand.h"
#include "ShareByUrlCommand.h"

#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Single-threaded COM objects are not properly supported on Windows CE platform, such as the Windows Mobile platforms that do not include full DCOM support. Define _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA to force ATL to support creating single-thread COM object's and allow use of it's single-threaded COM object implementations. The threading model in your rgs file was set to 'Free' as that is the only threading model supported in non DCOM Windows CE platforms."
#endif

enum struct CommandId
{
    ShareByUrl,
    MoveToDrive,
};

class ATL_NO_VTABLE CContextMenuHandler :
    public ATL::CComObjectRootEx<ATL::CComSingleThreadModel>,
    public ATL::CComCoClass<CContextMenuHandler, &CLSID_ContextMenuHandler>,
    public IShellExtInit,
    public IContextMenu
{
    struct MenuItem
    {
        UINT HeaderStringId = 0;
        UINT DescriptionStringId = 0;
        std::wstring Verb;
        std::function<const ContextMenuCommandBase&(const CContextMenuHandler&)> GetCommand;
    };

public:
    CContextMenuHandler();

    // IShellExtInit
    IFACEMETHODIMP Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY hkeyProgID) override;

    // IContextMenu
    IFACEMETHODIMP QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags) override;
    IFACEMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO pici) override;
    IFACEMETHODIMP GetCommandString(UINT_PTR idCmd, UINT uType, UINT* pRes, LPSTR pszName, UINT cchMax) override;

    DECLARE_REGISTRY_RESOURCEID(IDR_CONTEXTMENUHANDLER)

    DECLARE_NOT_AGGREGATABLE(CContextMenuHandler)

    BEGIN_COM_MAP(CContextMenuHandler)
        COM_INTERFACE_ENTRY(IShellExtInit)
        COM_INTERFACE_ENTRY(IContextMenu)
    END_COM_MAP()

    DECLARE_PROTECT_FINAL_CONSTRUCT()

private:
    ATL::CComPtr<IShellItemArray> m_selectedShellItems;
    SharedBitmapHandle m_iconBitmapHandle = nullptr;
    std::map<ULONG, CommandId> m_commandIdMap;
    std::unique_ptr<const ShareByUrlCommand> m_shareByUrlCommand;
    std::unique_ptr<const MoveToDriveCommand> m_moveToDriveCommand;

    static std::map<CommandId, MenuItem> s_menuItemMap;

    void InsertDriveMenuItem(
        _In_ HMENU menuHandle,
        _In_ CommandId commandId,
        _Inout_ UINT& menuItemIndex,
        _In_ UINT menuCommandId,
        _Inout_ UINT& menuCommandIdOffset);

    void SetMenuItemIcon(_In_ MENUITEMINFO& menuItemInfo);
    
    static HRESULT LoadDescription(_In_ CommandId commandId, _Out_writes_(cchMax) LPWSTR pszName, _In_ UINT cchMax);
    static HRESULT LoadVerb(_In_ CommandId commandId, _Out_writes_(cchMax) LPWSTR pszName, _In_ UINT cchMax);
};

OBJECT_ENTRY_AUTO(__uuidof(ContextMenuHandler), CContextMenuHandler)

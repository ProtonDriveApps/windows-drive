#include "pch.h"
#include "ContextMenuHandler.h"

#include "graphics.h"

using namespace std;
using namespace ATL;
using namespace nlohmann;

std::map<CommandId, CContextMenuHandler::MenuItem> CContextMenuHandler::s_menuItemMap =
{
    {
        CommandId::ShareByUrl,
        {
            IDS_SHARE_BY_LINK_MENU_ITEM_HEADER,
            IDS_SHARE_BY_LINK_DESCRIPTION,
            L"shareByProtonDriveUrl",
            [](const CContextMenuHandler& x) -> const ContextMenuCommandBase& { return *x.m_shareByUrlCommand; }
        }
    },
    {
        CommandId::MoveToDrive,
        {
            IDS_MOVE_TO_DRIVE_MENU_ITEM_HEADER,
            IDS_MOVE_TO_DRIVE_DESCRIPTION,
            L"moveToProtonDrive",
            [](const CContextMenuHandler& x) -> const ContextMenuCommandBase& { return *x.m_moveToDriveCommand; }
        }
    }
};

CContextMenuHandler::CContextMenuHandler() = default;

IFACEMETHODIMP CContextMenuHandler::Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY /*hkeyProgID*/)
{
    const auto result = SHCreateShellItemArrayFromDataObject(pdtobj, IID_PPV_ARGS(&m_selectedShellItems));
    if (FAILED(result))
    {
        return result;
    }

    if (!m_selectedShellItems)
    {
        return E_FAIL;
    }

    m_shareByUrlCommand = make_unique<ShareByUrlCommand>(m_selectedShellItems);
    m_moveToDriveCommand = make_unique<MoveToDriveCommand>(m_selectedShellItems);

    return S_OK;
}

IFACEMETHODIMP CContextMenuHandler::QueryContextMenu(HMENU hmenu, UINT indexMenu, const UINT idCmdFirst, const UINT /*idCmdLast*/, const UINT uFlags)
{
    _ATLTRY
    {
        if (!m_selectedShellItems || uFlags & (CMF_DEFAULTONLY | CMF_OPTIMIZEFORINVOKE))
        {
            return E_FAIL;
        }

        static constexpr array CommandIds = {CommandId::ShareByUrl, CommandId::MoveToDrive};

        auto menuCommandIdOffset = 0U;

        for (const auto commandId : CommandIds)
        {
            InsertDriveMenuItem(hmenu, commandId, indexMenu, idCmdFirst, menuCommandIdOffset);
        }

        return MAKE_HRESULT(SEVERITY_SUCCESS, FACILITY_NULL, static_cast<USHORT>(menuCommandIdOffset));
    }
    _ATLCATCH(e) { return e; }
    _ATLCATCHALL() { return E_FAIL; }
}

IFACEMETHODIMP CContextMenuHandler::InvokeCommand(LPCMINVOKECOMMANDINFO pici)
{
    if (!pici)
    {
        return E_INVALIDARG;
    }

    try
    {
        _ATLTRY
        {
            const auto commandIdIterator = m_commandIdMap.find(LOWORD(pici->lpVerb));
            if (commandIdIterator == m_commandIdMap.end())
            {
                return E_FAIL;
            }

            s_menuItemMap[commandIdIterator->second].GetCommand(*this).Execute();

            // TODO: handle failure and display message box

            return S_OK;
        }
            _ATLCATCH(e)
        {
            return e;
        }
    }
    catch (exception&)
    {
        return E_FAIL;
    }
}

IFACEMETHODIMP CContextMenuHandler::GetCommandString(const UINT_PTR idCmd, const UINT uType, UINT* pRes, LPSTR pszName, const UINT cchMax)
{
    HRESULT result;

    const auto commandIdIterator = m_commandIdMap.find(static_cast<ULONG>(idCmd));
    if (commandIdIterator == m_commandIdMap.end())
    {
        return E_INVALIDARG;
    }

    const auto commandId = commandIdIterator->second;

    switch (uType)
    {
    case GCS_HELPTEXTW:
        result = LoadDescription(commandId, reinterpret_cast<LPWSTR>(pszName), cchMax);
        break;

    case GCS_VERBW:
        result = LoadVerb(commandId, reinterpret_cast<LPWSTR>(pszName), cchMax);
        break;

    default:
        result = E_INVALIDARG;
    }

    return result;
}

void CContextMenuHandler::InsertDriveMenuItem(
    _In_ HMENU menuHandle,
    _In_ const CommandId commandId,
    _Inout_ UINT& menuItemIndex,
    _In_ const UINT firstMenuCommandId,
    _Inout_ UINT& menuCommandIdOffset)
{
    const auto& command = s_menuItemMap[commandId].GetCommand(*this);

    if (!command.CanExecute())
    {
        return;
    }

    MENUITEMINFO menuItemInfo;

    wchar_t menuName[64] = { 0 };
    LoadString(_AtlBaseModule.GetModuleInstance(), s_menuItemMap[commandId].HeaderStringId, menuName, ARRAYSIZE(menuName));

    menuItemInfo.cbSize = sizeof(MENUITEMINFO);
    menuItemInfo.fMask = MIIM_STRING | MIIM_FTYPE | MIIM_ID | MIIM_STATE;
    menuItemInfo.wID = firstMenuCommandId + menuCommandIdOffset;
    menuItemInfo.fType = MFT_STRING;
    menuItemInfo.dwTypeData = static_cast<PWSTR>(menuName);
    menuItemInfo.fState = MFS_ENABLED;

    SetMenuItemIcon(menuItemInfo);

    if (!InsertMenuItem(menuHandle, menuItemIndex, TRUE, &menuItemInfo))
    {
        AtlThrowLastWin32();
    }

    ++menuItemIndex;
    m_commandIdMap[menuCommandIdOffset++] = commandId;
}

void CContextMenuHandler::SetMenuItemIcon(_In_ MENUITEMINFO& menuItemInfo)
{
    if (!m_iconBitmapHandle)
    {
        const auto iconWidth = GetSystemMetrics(SM_CXSMICON);
        const auto iconHeight = GetSystemMetrics(SM_CYSMICON);

        const auto iconHandle = static_cast<IconHandle>(static_cast<HICON>(LoadImage(
            _AtlBaseModule.GetModuleInstance(),
            MAKEINTRESOURCE(IDI_ICON),
            IMAGE_ICON,
            iconWidth,
            iconHeight,
            LR_DEFAULTCOLOR)));

        if (iconHandle)
        {
            m_iconBitmapHandle = ConvertIconToBitmap(iconHandle.get(), iconWidth, iconHeight);
        }
    }

    if (m_iconBitmapHandle)
    {
        menuItemInfo.fMask |= MIIM_BITMAP;
        menuItemInfo.hbmpItem = m_iconBitmapHandle.get();
    }
}

HRESULT CContextMenuHandler::LoadDescription(_In_ const CommandId commandId, _Out_writes_(cchMax) LPWSTR pszName, _In_ const UINT cchMax)
{
    const auto descriptionStringIdIterator = s_menuItemMap.find(commandId);
    if (descriptionStringIdIterator == s_menuItemMap.end())
    {
        return E_INVALIDARG;
    }

    wchar_t stringBuffer[64] = { 0 };

    LoadString(_AtlBaseModule.GetModuleInstance(), descriptionStringIdIterator->second.DescriptionStringId, stringBuffer, ARRAYSIZE(stringBuffer));

    return StringCchCopyW(pszName, cchMax, stringBuffer);
}

HRESULT CContextMenuHandler::LoadVerb(_In_ const CommandId commandId, _Out_writes_(cchMax) LPWSTR pszName, _In_ const UINT cchMax)
{
    const auto verbIterator = s_menuItemMap.find(commandId);
    if (verbIterator == s_menuItemMap.end())
    {
        return E_INVALIDARG;
    }

    return StringCchCopyW(pszName, cchMax, verbIterator->second.Verb.c_str());
}

// CContextMenuHandler

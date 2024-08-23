#include "pch.h"
#include "MoveToDriveCommand.h"

#include "ipc.h"

using namespace std;
using namespace ATL;
using namespace nlohmann;

enum struct SyncRootType
{
    CloudFiles = 1,
    HostDeviceFolder = 2,
    ForeignDevice = 3,
};

struct SyncRootPathsQueryRequest : IpcMessage<vector<SyncRootType>>
{
    explicit SyncRootPathsQueryRequest(const vector<SyncRootType>& syncRootTypes) : IpcMessage(L"SyncRootPathsQuery", syncRootTypes) {}
};

bool TryParsePathAsShellItem(_In_ const wstring& path, _Out_ CComPtr<IShellItem>& shellItem)
{
    const auto result = SHCreateItemFromParsingName(path.c_str(), nullptr, IID_PPV_ARGS(&shellItem));
    return SUCCEEDED(result);
}

bool TryParsePathAsItemIdList(_In_ const wstring& path, _Out_ CComHeapPtr<__unaligned ITEMIDLIST_ABSOLUTE>& itemIdList)
{
    const auto result = ILCreateFromPath(path.c_str());
    if (result == nullptr)
    {
        return false;
    }

    itemIdList.Attach(result);
    return true;
};

template <typename T>
bool TryGetSyncRootItems(_In_ const vector<SyncRootType>& syncRootTypes, _In_ auto& parsePath, _Out_ vector<T>& rootItems)
{
    const auto request = SyncRootPathsQueryRequest(syncRootTypes);

    vector<wstring> syncRootPaths;
    if (!TrySendIpcMessage(request, syncRootPaths) || syncRootPaths.empty())
    {
        return false;
    }

    rootItems = vector<T>(syncRootPaths.size());
    for (unsigned int i = 0; i < syncRootPaths.size(); ++i)
    {
        T rootItem;
        auto result = parsePath(syncRootPaths[i], rootItems[i]);
        if (!result)
        {
            return false;
        }
    }

    return true;
}

bool CanMove(IShellItem& item)
{
    SFGAOF attributes;
    const auto result = item.GetAttributes(SFGAO_CANMOVE, &attributes);
    if (FAILED(result))
    {
        return false;
    }

    return (attributes & SFGAO_CANMOVE) != 0;
}

MoveToDriveCommand::MoveToDriveCommand(_In_ const CComPtr<IShellItemArray>& selectedShellItems)
    : ContextMenuCommandBase(selectedShellItems)
{
}

bool MoveToDriveCommand::CanExecute() const
{
    vector<CComHeapPtr<__unaligned ITEMIDLIST_ABSOLUTE>> rootItemIdLists;
    if (!TryGetSyncRootItems({ SyncRootType::CloudFiles, SyncRootType::HostDeviceFolder, SyncRootType::ForeignDevice }, TryParsePathAsItemIdList, rootItemIdLists))
    {
        return false;
    }

    const auto canMoveAndNoRootItemIsRelated = [rootItemIdLists](IShellItem& selectedItem)
    {
        if (!CanMove(selectedItem))
        {
            return false;
        }

        SFGAOF attributes;
        if (FAILED(selectedItem.GetAttributes(SFGAO_FILESYSTEM, &attributes)) || attributes == 0)
        {
            return false;
        }

        CComHeapPtr<WCHAR> selectedItemPath;
        const auto result = selectedItem.GetDisplayName(SIGDN_FILESYSPATH, &selectedItemPath);
        ATLENSURE_SUCCEEDED(result);

        CComHeapPtr<__unaligned ITEMIDLIST_ABSOLUTE> selectedItemIdList;
        if (!TryParsePathAsItemIdList(wstring(selectedItemPath), selectedItemIdList))
        {
            return false;
        }

        const auto isRelated = [&selectedItemIdList](const CComHeapPtr<__unaligned ITEMIDLIST_ABSOLUTE>& rootItemIdList) -> bool
        {
            const auto isEqual = ILIsEqual(rootItemIdList, selectedItemIdList);
            if (isEqual)
            {
                return true;
            }

            const auto isAncestor = ILIsParent(rootItemIdList, selectedItemIdList, false);
            if (isAncestor)
            {
                return true;
            }

            const auto isDescendent = ILIsParent(selectedItemIdList, rootItemIdList, false);
            return isDescendent;
        };

        const auto noRelatedRootItemFound = !ranges::any_of(rootItemIdLists, isRelated);
        return noRelatedRootItemFound;
    };

    const auto result = IsTrueForAllSelectedItems(canMoveAndNoRootItemIsRelated);
    return result;
}

void MoveToDriveCommand::Execute() const
{
    vector<CComPtr<IShellItem>> syncRootItems;
    if (!TryGetSyncRootItems({ SyncRootType::CloudFiles }, TryParsePathAsShellItem, syncRootItems))
    {
        return;
    }

    if (syncRootItems.empty())
    {
        return;
    }

    const auto& cloudFilesRootItem = syncRootItems[0];

    CComPtr<IFileOperation> fileOperation;
    auto result = CoCreateInstance(__uuidof(FileOperation), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&fileOperation));
    ATLENSURE_SUCCEEDED(result);

    result = fileOperation->MoveItems(m_selectedShellItems, cloudFilesRootItem);
    ATLENSURE_SUCCEEDED(result);

    result = fileOperation->PerformOperations();
    ATLENSURE_SUCCEEDED(result);
}

bool MoveToDriveCommand::IsTrueForAllSelectedItems(_In_ auto& predicate) const
{
    CComPtr<IEnumShellItems> shellItemEnumerator;
    auto result = m_selectedShellItems->EnumItems(&shellItemEnumerator);
    ATLENSURE_SUCCEEDED(result);

    while (true)
    {
        CComPtr<IShellItem> currentSelectedItem;
        ULONG numberOfItemsFetched;
        result = shellItemEnumerator->Next(1, &currentSelectedItem, &numberOfItemsFetched);
        ATLENSURE_SUCCEEDED(result);

        if (numberOfItemsFetched <= 0)
        {
            break;
        }

        if (!predicate(*currentSelectedItem))
        {
            return false;
        }
    }

    return true;
}
#include "pch.h"
#include "ShareByUrlCommand.h"
#include "ipc.h"

using namespace std;
using namespace ATL;
using namespace nlohmann;

struct ShareByUrlCommandRequest : IpcMessage<wstring>
{
    ShareByUrlCommandRequest(const wstring& path) : IpcMessage<wstring>(L"ShareByUrlCommand", path) {}
};

struct RemoteIdsQueryRequest : IpcMessage<wstring>
{
    RemoteIdsQueryRequest(const wstring& path) : IpcMessage<wstring>(L"RemoteIdsQuery", path) {}
};

struct RemoteIdsQueryResponse
{
    wstring shareId;
    wstring linkId;
};

void from_json(const json& j, optional<RemoteIdsQueryResponse>& response) {
    if (j.empty())
    {
        return;
    }
    
    response.emplace(
        j.at(NAMEOF(response.value().shareId)).get<wstring>(),
        j.at(NAMEOF(response.value().linkId)).get<wstring>());
}

ShareByUrlCommand::ShareByUrlCommand(const CComPtr<IShellItemArray>& selectedShellItems)
: ContextMenuCommandBase(selectedShellItems)
{
}

bool ShareByUrlCommand::CanExecute() const
{
    wstring path;
    if (!TryGetSingleSelectedItemPath(path))
    {
        return false;
    }

    return HasRemoteCounterpart(path);
}

void ShareByUrlCommand::Execute() const
{
    wstring path;
    if (!TryGetSingleSelectedItemPath(path))
    {
        return;
    }

    if (!TrySendIpcMessage(ShareByUrlCommandRequest(wstring(path))))
    {
        AtlThrowLastWin32();
    }
}

bool ShareByUrlCommand::TryGetSingleSelectedItemPath(_Out_ wstring& path) const
{
    CComPtr<IShellItem> selectedItem;
    if (!TryGetSingleSelectedItem(selectedItem))
    {
        return false;
    }

    CComHeapPtr<WCHAR> mappingQueryPathPointer;
    const auto result = selectedItem->GetDisplayName(SIGDN_FILESYSPATH, &mappingQueryPathPointer);
    if (FAILED(result))
    {
        return false;
    }

    path = mappingQueryPathPointer;

    return true;
}

bool ShareByUrlCommand::TryGetSingleSelectedItem(_Out_ CComPtr<IShellItem>& item) const
{
    DWORD numberOfItems;
    auto result = m_selectedShellItems->GetCount(&numberOfItems);
    ATLENSURE_SUCCEEDED(result);

    if (numberOfItems > 1)
    {
        return false;
    }
    
    result = m_selectedShellItems->GetItemAt(0, &item);
    ATLENSURE_SUCCEEDED(result);

    return true;
}

bool ShareByUrlCommand::HasRemoteCounterpart(_In_ const std::wstring& path) const
{
    const auto request = RemoteIdsQueryRequest(path);
    optional<RemoteIdsQueryResponse> response;
    if (!TrySendIpcMessage(request, response))
    {
        return false;
    }

    if (!response.has_value())
    {
        return false;
    }

    return response.value().linkId.length() > 0;
}

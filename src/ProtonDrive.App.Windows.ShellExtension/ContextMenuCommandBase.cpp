#include "pch.h"
#include "ContextMenuCommandBase.h"

ContextMenuCommandBase::ContextMenuCommandBase(const ATL::CComPtr<IShellItemArray>& selectedShellItems)
{
    m_selectedShellItems = selectedShellItems;
}

ContextMenuCommandBase::~ContextMenuCommandBase() = default;

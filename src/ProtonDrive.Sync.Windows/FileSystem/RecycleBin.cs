using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Windows.FileSystem.Client;
using Vanara.PInvoke;

namespace ProtonDrive.Sync.Windows.FileSystem;

public static class RecycleBin
{
    private static readonly StaTaskScheduler TaskScheduler = new(numberOfThreads: 1);

    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo

    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming convention")]
    private static readonly Guid IID_IShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    // ReSharper restore InconsistentNaming
    // ReSharper restore IdentifierTypo

    public static async Task MoveToRecycleBinAsync(string path)
    {
        await Task.Factory.StartNew(
            () =>
            {
                var fileOperation = FileOperation.GetInstance();

                try
                {
                    Shell32.SHCreateItemFromParsingName(path, default, in IID_IShellItem, out var item).ThrowExceptionForHR();

                    if (item is null)
                    {
                        throw new InvalidOperationException();
                    }

                    try
                    {
                        var progressSink = new FileOperationProgressSink();

                        fileOperation.SetOperationFlags(Shell32.FILEOP_FLAGS.FOFX_RECYCLEONDELETE | Shell32.FILEOP_FLAGS.FOF_NO_UI);

                        fileOperation.DeleteItem((Shell32.IShellItem)item, progressSink);

                        fileOperation.PerformOperations();

                        progressSink.DeletionResult.ThrowExceptionForHR();
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(item);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(fileOperation);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler).ConfigureAwait(false);
    }

    private static class FileOperation
    {
        // ReSharper disable InconsistentNaming
        // ReSharper disable IdentifierTypo

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 naming convention")]
        private static readonly Guid CLSID_FileOperation = new("3AD05575-8857-4850-9277-11B85BDB8E09");

        // ReSharper restore InconsistentNaming
        // ReSharper restore IdentifierTypo

        private static readonly Type? Type = Type.GetTypeFromCLSID(CLSID_FileOperation);

        public static Shell32.IFileOperation GetInstance()
        {
            if (Type is null)
            {
                throw new TypeLoadException("Could not get type of IFileOperation");
            }

            return (Shell32.IFileOperation)(Activator.CreateInstance(Type)
                                            ?? throw new NotSupportedException("Could not get instance of IFileOperation"));
        }
    }

    private class FileOperationProgressSink : Shell32.IFileOperationProgressSink
    {
        public HRESULT DeletionResult { get; private set; }

        HRESULT Shell32.IFileOperationProgressSink.StartOperations()
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.FinishOperations(HRESULT hrResult)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PreRenameItem(Shell32.TRANSFER_SOURCE_FLAGS dwFlags, Shell32.IShellItem psiItem, string pszNewName)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PostRenameItem(
            Shell32.TRANSFER_SOURCE_FLAGS dwFlags,
            Shell32.IShellItem psiItem,
            string pszNewName,
            HRESULT hrRename,
            Shell32.IShellItem psiNewlyCreated)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PreMoveItem(
            Shell32.TRANSFER_SOURCE_FLAGS dwFlags,
            Shell32.IShellItem psiItem,
            Shell32.IShellItem psiDestinationFolder,
            string? pszNewName)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PostMoveItem(
            Shell32.TRANSFER_SOURCE_FLAGS dwFlags,
            Shell32.IShellItem psiItem,
            Shell32.IShellItem psiDestinationFolder,
            string pszNewName,
            HRESULT hrMove,
            Shell32.IShellItem psiNewlyCreated)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PreCopyItem(
            Shell32.TRANSFER_SOURCE_FLAGS dwFlags,
            Shell32.IShellItem psiItem,
            Shell32.IShellItem psiDestinationFolder,
            string? pszNewName)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PostCopyItem(
            Shell32.TRANSFER_SOURCE_FLAGS dwFlags,
            Shell32.IShellItem psiItem,
            Shell32.IShellItem psiDestinationFolder,
            string pszNewName,
            HRESULT hrCopy,
            Shell32.IShellItem psiNewlyCreated)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PreDeleteItem(Shell32.TRANSFER_SOURCE_FLAGS dwFlags, Shell32.IShellItem psiItem)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PostDeleteItem(
            Shell32.TRANSFER_SOURCE_FLAGS dwFlags,
            Shell32.IShellItem psiItem,
            HRESULT hrDelete,
            Shell32.IShellItem? psiNewlyCreated)
        {
            DeletionResult = hrDelete;
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PreNewItem(Shell32.TRANSFER_SOURCE_FLAGS dwFlags, Shell32.IShellItem psiDestinationFolder, string pszNewName)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PostNewItem(
            Shell32.TRANSFER_SOURCE_FLAGS dwFlags,
            Shell32.IShellItem psiDestinationFolder,
            string pszNewName,
            string? pszTemplateName,
            uint dwFileAttributes,
            HRESULT hrNew,
            Shell32.IShellItem psiNewItem)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.UpdateProgress(uint iWorkTotal, uint iWorkSoFar)
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.ResetTimer()
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.PauseTimer()
        {
            return HRESULT.S_OK;
        }

        HRESULT Shell32.IFileOperationProgressSink.ResumeTimer()
        {
            return HRESULT.S_OK;
        }
    }
}

// Copyright © .NET Foundation and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE-MIT file in the project root for full license information.
//
// Adapted from https://github.com/dotnet/pinvoke/blob/master/src/Windows.Core/HResult%2BCode.cs

namespace ProtonDrive.Sync.Windows.Interop;

/// <content>
/// The <see cref="Code"/> nested type.
/// </content>
internal partial struct HResult
{
    // ReSharper disable UnusedMember.Global
    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    // ReSharper disable CommentTypo

    /// <summary>
    /// Common HRESULT constants.
    /// </summary>
    public enum Code : uint
    {
        /// <summary>
        /// Operation successful, and returned a false result.
        /// </summary>
        S_FALSE = 1,

        /// <summary>
        /// Operation successful
        /// </summary>
        S_OK = 0,

        /// <summary>
        /// Unspecified failure
        /// </summary>
        E_FAIL = 0x80004005,

        /// <summary>
        /// Operation aborted
        /// </summary>
        E_ABORT = 0x80004004,

        /// <summary>
        /// General access denied error
        /// </summary>
        E_ACCESSDENIED = 0x80070005,

        /// <summary>
        /// Handle that is not valid
        /// </summary>
        E_HANDLE = 0x80070006,

        /// <summary>
        /// One or more arguments are not valid
        /// </summary>
        E_INVALIDARG = 0x80070057,

        /// <summary>
        /// No such interface supported
        /// </summary>
        E_NOINTERFACE = 0x80004002,

        /// <summary>
        /// Not implemented
        /// </summary>
        E_NOTIMPL = 0x80004001,

        /// <summary>
        /// Failed to allocate necessary memory
        /// </summary>
        E_OUTOFMEMORY = 0x8007000E,

        /// <summary>
        /// Pointer that is not valid
        /// </summary>
        E_POINTER = 0x80004003,

        /// <summary>
        /// Unexpected failure
        /// </summary>
        E_UNEXPECTED = 0x8000FFFF,

        /// <summary>
        /// The call was already canceled
        /// </summary>
        RPC_E_CALL_CANCELED = 0x80010002,

        /// <summary>
        /// The call was completed during the timeout interval
        /// </summary>
        RPC_E_CALL_COMPLETE = 0x80010117,

        /// <summary>
        /// Call cancellation is not enabled on the specified thread
        /// </summary>
        CO_E_CANCEL_DISABLED = 0x80010140,

        /// <summary>
        /// Item could not be found
        /// </summary>
        STG_E_FILENOTFOUND = 0x80030002,

        /// <summary>
        /// The Shell item does not support thumbnail extraction. For example, .exe or .lnk items.
        /// </summary>
        WTS_E_FAILEDEXTRACTION = 0x8004B200,

        /// <summary>
        /// The extraction took longer than the maximum allowable time. The extraction was not completed.
        /// </summary>
        WTS_E_EXTRACTIONTIMEDOUT = 0x8004B201,

        /// <summary>
        /// A surrogate process was not available to be used for the extraction process.
        /// </summary>
        WTS_E_SURROGATEUNAVAILABLE = 0x8004B202,

        /// <summary>
        /// The WTS_FASTEXTRACT flag was set, but fast extraction is not available.
        /// </summary>
        WTS_E_FASTEXTRACTIONNOTSUPPORTED = 0x8004B203,

        WTS_E_DATAFILEUNAVAILABLE = 0x8004B204,
        WTS_E_EXTRACTIONPENDING = 0x8004B205,
        WTS_E_EXTRACTIONBLOCKED = 0x8004B206,
        WTS_E_NOSTORAGEPROVIDERTHUMBNAILHANDLER = 0x8004B207,
    }
}

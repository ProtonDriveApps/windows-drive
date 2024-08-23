// Copyright © .NET Foundation and Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE-MIT file in the project root for full license information.
//
// Adapted from https://github.com/dotnet/pinvoke/blob/master/src/Windows.Core/HResult%2BSeverityCode.cs

namespace ProtonDrive.App.Windows.Interop;

/// <content>
/// The <see cref="SeverityCode"/> nested type.
/// </content>
internal partial struct HResult
{
    /// <summary>
    /// HRESULT severity codes defined by winerror.h.
    /// </summary>
    public enum SeverityCode : uint
    {
        Success = 0,
        Fail = 0x80000000,
    }
}

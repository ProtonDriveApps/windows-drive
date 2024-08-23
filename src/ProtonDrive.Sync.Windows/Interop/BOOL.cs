// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.

namespace ProtonDrive.Sync.Windows.Interop;

/// <summary>
/// Blittable version of Windows BOOL type. It is convenient in situations where
/// manual marshalling is required, or to avoid overhead of regular bool marshalling.
/// </summary>
/// <remarks>
/// Some Windows APIs return arbitrary integer values although the return type is defined
/// as BOOL. It is best to never compare BOOL to TRUE. Always use bResult != BOOL.FALSE
/// or bResult == BOOL.FALSE .
/// </remarks>
public enum BOOL
{
    FALSE = 0,
    TRUE = 1,
}

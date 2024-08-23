// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.

using System;

namespace ProtonDrive.Sync.Windows.FileSystem.Internal;

/// <summary>
/// Simple wrapper to safely disable the normal media insertion prompt for
/// removable media (floppies, cds, memory cards, etc.)
/// </summary>
/// <remarks>
/// Note that removable media file systems lazily load. After starting the OS
/// they won't be loaded until you have media in the drive- and as such the
/// prompt won't happen. You have to have had media in at least once to get
/// the file system to load and then have removed it.
/// </remarks>
internal struct DisableMediaInsertionPrompt : IDisposable
{
    private bool _succeeded;
    private uint _previousMode;

    public static DisableMediaInsertionPrompt Create()
    {
        DisableMediaInsertionPrompt prompt = default;
        prompt._succeeded = Interop.Kernel32.SetThreadErrorMode(Interop.Kernel32.SEM_FAILCRITICALERRORS, out prompt._previousMode);
        return prompt;
    }

    public void Dispose()
    {
        if (_succeeded)
            Interop.Kernel32.SetThreadErrorMode(_previousMode, out _);
    }
}

using System;

namespace ProtonDrive.Shared.IO;

/// <summary>
/// The state of a placeholder file or folder
/// </summary>
[Flags]
public enum PlaceholderState
{
    /// <summary>
    /// Is not a placeholder
    /// </summary>
    NoStates = 0x00000000,

    /// <summary>
    /// Is a placeholder
    /// </summary>
    Placeholder = 0x00000001,

    /// <summary>
    /// The directory is both a placeholder directory as well as the sync root
    /// </summary>
    SyncRoot = 0x00000002,

    /// <summary>
    /// The file or directory must be a placeholder and there exists an essential property in the property store of the file or directory
    /// </summary>
    EssentialPropertyPresent = 0x00000004,

    /// <summary>
    /// The file or directory must be a placeholder and its content in sync with the cloud
    /// </summary>
    InSync = 0x00000008,

    /// <summary>
    /// The file or directory must be a placeholder and its content is not ready to be consumed by the user application,
    /// though it may or may not be fully present locally. An example is a placeholder file whose content has been fully
    /// downloaded to the local disk, but is yet to be validated by a sync provider that has registered the sync root
    /// with the hydration modifier VERIFICATION_REQUIRED.
    /// </summary>
    Partial = 0x00000010,

    /// <summary>
    /// The file or directory must be a placeholder and its content is not fully present locally. When this is set,
    /// <see cref="Partial"/> must also be set.
    /// </summary>
    PartiallyOnDisk = 0x00000020,

    /// <summary>
    /// This is an invalid state when the API fails to parse the information of the file or directory
    /// </summary>
    Invalid = unchecked((int)0xffffffff),
}

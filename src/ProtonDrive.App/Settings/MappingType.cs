namespace ProtonDrive.App.Settings;

public enum MappingType
{
    /// <summary>
    /// Maps local cloud files folder to remote one.
    /// <para>Local folder is the "My files" subfolder of the account root folder.</para>
    /// <para>Remote folder is a root folder of the default share of the user account.</para>
    /// </summary>
    CloudFiles = 1,

    /// <summary>
    /// Maps local host device folder to remote one. A host device is a device the app is running on.
    /// <para>Local folder is a known or arbitrary folder chosen by the user on this device.</para>
    /// <para>Remote folder is a first level folder on the remote host device.</para>
    /// </summary>
    HostDeviceFolder = 2,

    /// <summary>
    /// Maps foreign device root folder to foreign device.
    /// <para>Local folder is the subfolder of the foreign devices' folder. Foreign devices folder is
    /// the "Other computers" subfolder of the account root folder.</para>
    /// <para>Remote folder is a root folder on the remote foreign device.</para>
    /// </summary>
    ForeignDevice = 3,

    /// <summary>
    /// Maps local shared with me items (folder or file) to remote ones.
    /// <para>Local item (folder or file) is the child of the "Shared with me" folder, which is a subfolder of the account root folder.</para>
    /// <para>Remote counterpart is an item (folder or file) on a foreign volume, that another user shared with me.</para>
    /// </summary>
    SharedWithMeItem = 4,

    /// <summary>
    /// Represents the local "Shared with me" folder, which contains shared with me files and folders.
    /// Does not map to the remote part.
    /// <para>Local folder is the subfolder of the account root folder.</para>
    /// </summary>
    SharedWithMeRootFolder = 102,
}

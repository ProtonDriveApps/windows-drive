using System;

namespace ProtonDrive.Sync.Engine.Shared;

[Flags]
public enum UpdateStatus
{
    Unchanged = 0b_00000000,
    Created = 0b_00000001,
    Edited = 0b_00000010,
    Renamed = 0b_00000100,
    Moved = 0b_00001000,
    Deleted = 0b_00010000,
    All = 0b_00011111,

    RenamedAndMoved = Renamed | Moved,

    Restore = 0b_00100000,
}

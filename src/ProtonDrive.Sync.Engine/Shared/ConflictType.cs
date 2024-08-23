namespace ProtonDrive.Sync.Engine.Shared;

public enum ConflictType
{
    None,
    CreateCreate,
    CreateCreatePseudo,
    CreateParentDelete,
    DeleteDeletePseudo,
    EditDelete,
    EditEdit,
    EditEditPseudo,
    EditParentDelete,
    MoveCreate,
    MoveDelete,
    MoveMove,
    MoveMoveCycle,
    MoveMoveDest,
    MoveMovePseudo,
    MoveMoveSource,
    MoveParentDeleteDest,
}

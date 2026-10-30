namespace TabDock.Core.Services;

public enum ExclusionReason
{
    None,
    OwnProcess,
    NotVisible,
    Cloaked,
    HasOwner,
    ToolWindow,
    ExcludedClass,
    EmptyTitle,
    TooSmall,
    ProcessInaccessible,
    ElevatedProcess,
}

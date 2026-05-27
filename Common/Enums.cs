namespace AutoCAC.Common;

public enum ReadStateFilter
{
    Unread,
    Read,
    All
}

public enum ActivityLogType
{
    Unknown,
    Created,
    Comment,
    StatusChanged,
    Error,
    Warning,
    Assigned,
    Unassigned,
    Other
}
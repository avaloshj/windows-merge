namespace TabDock.App.Services;

public sealed class NoticeEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

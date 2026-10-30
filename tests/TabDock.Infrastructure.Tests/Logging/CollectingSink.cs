using Serilog.Core;
using Serilog.Events;

namespace TabDock.Infrastructure.Tests.Logging;

internal sealed class CollectingSink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}

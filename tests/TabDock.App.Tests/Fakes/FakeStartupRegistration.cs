using TabDock.Core;
using TabDock.Core.Abstractions;

namespace TabDock.App.Tests.Fakes;

internal sealed class FakeStartupRegistration : IStartupRegistration
{
    public bool Enabled { get; set; }

    public bool Fails { get; set; }

    public bool IsEnabled() => Enabled;

    public Result SetEnabled(bool enabled)
    {
        if (Fails)
            return Result.Fail(new DomainError("startup.registry", "Error_StartupRegistration"));

        Enabled = enabled;
        return Result.Success();
    }
}

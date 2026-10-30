namespace TabDock.Core.Services;

public readonly record struct EligibilityResult(EligibilityStatus Status, ExclusionReason Reason)
{
    public static EligibilityResult Eligible { get; } = new(EligibilityStatus.Eligible, ExclusionReason.None);

    public bool IsEligible => Status == EligibilityStatus.Eligible;

    public static EligibilityResult Excluded(ExclusionReason reason) => new(EligibilityStatus.Excluded, reason);

    public static EligibilityResult Disabled(ExclusionReason reason) => new(EligibilityStatus.Disabled, reason);
}

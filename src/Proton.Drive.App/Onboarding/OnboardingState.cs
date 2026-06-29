namespace Proton.Drive.App.Onboarding;

public sealed record OnboardingState(OnboardingStatus Status, OnboardingStep Step)
{
    public static OnboardingState Initial { get; } = new(default, default);
}

namespace ProtonDrive.Shared.Configuration;

public sealed class LocalFeatureFlags
{
    public bool UpgradeStorageOnboardingStepEnabled { get; internal set; }
    public bool? OffersEnabled { get; internal set; }
    public bool? DriveCryptoEncryptBlocksWithPgpAeadEnabled { get; internal set; }
    public bool FileConsistencyGuardRemoteApplicabilityForceEnabled { get; internal set; }
    public bool? FileConsistencyGuardEnabled { get; set; }
    public bool? FileConsistencyGuardDownloadWave1Enabled { get; set; }
    public bool? FileConsistencyGuardDownloadWave2Enabled { get; set; }
    public bool? FileConsistencyGuardSanitizationEnabled { get; internal set; }

    // When the feature flag is not present (null), it has no effect.
    // When the feature flag has a value, it overrides the corresponding remote feature flag, if any.
    // Example of feature flag property:
    // public bool? SomeFeatureEnabled { get; internal set; }
}

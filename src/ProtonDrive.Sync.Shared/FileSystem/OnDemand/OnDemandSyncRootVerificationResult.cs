namespace ProtonDrive.Sync.Shared.FileSystem.OnDemand;

public sealed record OnDemandSyncRootVerificationResult(OnDemandSyncRootVerificationVerdict Verdict, string? ConflictingProviderName = null);

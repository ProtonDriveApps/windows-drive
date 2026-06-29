using Proton.Drive.Shared;

namespace Proton.Drive.App;

public sealed record AppArguments(AppLaunchMode LaunchMode, AppCrashMode CrashMode);

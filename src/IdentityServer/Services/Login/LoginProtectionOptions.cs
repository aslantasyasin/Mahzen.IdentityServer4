using System;

namespace IdentityServer.Services.Login;

public sealed class LoginProtectionOptions
{
    public const string Section = "LoginProtection";

    public LoginProtectionMode Mode { get; set; } = LoginProtectionMode.Off;
    public int SourceFailureLimit { get; set; } = 5;
    public TimeSpan SourceWindow { get; set; } = TimeSpan.FromMinutes(15);
    public int AccountFailureLimit { get; set; } = 20;
    public TimeSpan AccountWindow { get; set; } = TimeSpan.FromHours(1);
    public int DeviceFailureLimit { get; set; } = 10;
    public TimeSpan DeviceWindow { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan DeviceLifetime { get; set; } = TimeSpan.FromDays(90);
    public TimeSpan ChallengeLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public int ChallengeMaxAttempts { get; set; } = 5;
    public TimeSpan ChallengeResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
    public int ChallengeSendsPerHour { get; set; } = 5;
    public TimeSpan AttackNoticeInterval { get; set; } = TimeSpan.FromHours(24);
    public bool NotifyNewDevice { get; set; } = true;
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan UnderAttackDuration { get; set; } = TimeSpan.FromHours(24);
    public int StepUpFailureLimit { get; set; } = 10;
    public TimeSpan StepUpFailureWindow { get; set; } = TimeSpan.FromHours(1);
    public int MaxDevicesPerUser { get; set; } = 10;

    public bool IsValid() =>
        Enum.IsDefined(Mode) &&
        SourceFailureLimit > 0 && AccountFailureLimit > 0 && DeviceFailureLimit > 0 &&
        ChallengeMaxAttempts > 0 && ChallengeSendsPerHour > 0 &&
        SourceWindow > TimeSpan.Zero && AccountWindow > TimeSpan.Zero && DeviceWindow > TimeSpan.Zero &&
        DeviceLifetime > TimeSpan.Zero && ChallengeLifetime > TimeSpan.Zero &&
        ChallengeResendCooldown >= TimeSpan.Zero && AttackNoticeInterval > TimeSpan.Zero &&
        CleanupInterval > TimeSpan.Zero && UnderAttackDuration > TimeSpan.Zero &&
        StepUpFailureLimit > 0 && StepUpFailureWindow > TimeSpan.Zero && MaxDevicesPerUser > 0;
}

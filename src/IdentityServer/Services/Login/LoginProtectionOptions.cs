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

    public bool IsValid() =>
        Enum.IsDefined(Mode) &&
        SourceFailureLimit > 0 && AccountFailureLimit > 0 &&
        SourceWindow > TimeSpan.Zero && AccountWindow > TimeSpan.Zero;
}

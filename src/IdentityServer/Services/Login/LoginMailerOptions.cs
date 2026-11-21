using System;

namespace IdentityServer.Services.Login;

public sealed class LoginMailerOptions
{
    public const string Section = "Notification";

    public string BaseUrl { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}

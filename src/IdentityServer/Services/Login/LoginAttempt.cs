namespace IdentityServer.Services.Login;

public sealed record LoginAttempt(string UserId, string Email, string Source, long? DeviceId, bool IsAdmin, string StepUpId, string StepUpCode);

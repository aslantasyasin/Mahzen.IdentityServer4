using System;

namespace IdentityServer.Services.Login;

public enum LoginOutcomeKind
{
    Allowed,
    Rejected,
    StepUpRequired,
    StepUpFailed,
    TemporarilyUnavailable,
}

public sealed record LoginOutcome(LoginOutcomeKind Kind, string DeviceToken = null, Guid? ChallengeId = null)
{
    public static LoginOutcome Allowed(string deviceToken = null) => new(LoginOutcomeKind.Allowed, deviceToken);

    public static LoginOutcome StepUpRequired(Guid challengeId) => new(LoginOutcomeKind.StepUpRequired, ChallengeId: challengeId);

    public static LoginOutcome StepUpFailed() => new(LoginOutcomeKind.StepUpFailed);

    public static LoginOutcome Rejected() => new(LoginOutcomeKind.Rejected);

    public static LoginOutcome TemporarilyUnavailable() => new(LoginOutcomeKind.TemporarilyUnavailable);
}

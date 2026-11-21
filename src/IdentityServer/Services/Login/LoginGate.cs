namespace IdentityServer.Services.Login;

public enum LoginGateKind
{
    Open,
    StepUp,
    Unavailable,
}

public sealed record LoginGate(LoginGateKind Kind, string Reason = null)
{
    public static readonly LoginGate Open = new(LoginGateKind.Open);
}

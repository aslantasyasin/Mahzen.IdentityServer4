using System;

namespace IdentityServer.Services.Login;

public sealed record LoginChallenge(Guid Id, string UserId, string CodeHash, DateTimeOffset ExpiresAt, DateTimeOffset? LastSentAt, bool Used);

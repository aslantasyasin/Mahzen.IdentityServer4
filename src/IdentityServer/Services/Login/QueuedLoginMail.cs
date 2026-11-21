using System.Collections.Generic;

namespace IdentityServer.Services.Login;

public sealed record QueuedLoginMail(string To, string TemplateKey, IReadOnlyDictionary<string, string> Model);

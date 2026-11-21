using System.Collections.Generic;
using System.Threading.Tasks;

namespace IdentityServer.Services.Login;

public interface ILoginMailer
{
    Task<bool> SendAsync(string to, string templateKey, IReadOnlyDictionary<string, string> model);
}

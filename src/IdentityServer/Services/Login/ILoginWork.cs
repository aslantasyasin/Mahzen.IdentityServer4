using System;
using System.Threading.Tasks;

namespace IdentityServer.Services.Login;

public interface ILoginWork
{
    void Enqueue(string name, Func<LoginProtectionService, Task> job);
}

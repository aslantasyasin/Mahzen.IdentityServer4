using System;
using System.Threading.Tasks;

namespace IdentityServer.Services.Login
{
	public interface ILoginService
	{
		Task Login();
	}
}


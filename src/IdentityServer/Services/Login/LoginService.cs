using System;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;

namespace IdentityServer.Services.Login
{
	public class LoginService : ILoginService
    {
		public LoginService()
		{
		}

        public async Task Login()
        {
            var client = new HttpClient();

            var disco = await client.GetDiscoveryDocumentAsync("https://localhost:5001");

            var password = new PasswordTokenRequest();

            password.Address = disco.TokenEndpoint;
            password.UserName = "fcakiroglu@outlook.com";
            password.Password = "password";
            password.ClientId = "client2";

            var token = await client.RequestPasswordTokenAsync(password);
        }
    }
}


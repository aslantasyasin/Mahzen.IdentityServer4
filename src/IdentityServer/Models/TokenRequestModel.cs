namespace IdentityServer.Models
{
    public class TokenRequestModel
    {
        public string ClientId { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}

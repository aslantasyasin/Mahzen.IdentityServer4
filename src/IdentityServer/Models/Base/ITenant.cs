using System;
namespace IdentityServer.Models.Base
{
	public interface ITenant
	{
        public int TenantId { get; set; }
    }
}


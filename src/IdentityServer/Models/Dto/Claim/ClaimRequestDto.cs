using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.Claim
{
	public class ClaimRequestDto
	{
        public string ClaimType { get; set; }
        public string ClaimValue { get; set; }
    }
}


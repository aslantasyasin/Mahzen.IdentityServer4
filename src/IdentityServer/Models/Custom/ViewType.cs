using System;
using System.ComponentModel.DataAnnotations;
using IdentityServer.Models.Custom.Base;

namespace IdentityServer.Models.Custom
{
	public class ViewType : CustomBaseEntity
    {
        [Required]
		public string Name { get; set; }
        [Required]
        public string TrName { get; set; }
    }
}


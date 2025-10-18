using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Custom.Base
{
	public class CustomBaseEntity
	{
		[Key]
		public int Id { get; set; }
		[Required]
		public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
		public DateTime UpdateDate { get; set; }
	}
}


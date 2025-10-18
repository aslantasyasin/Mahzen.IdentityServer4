using System;
using System.ComponentModel.DataAnnotations;
using IdentityServer.Models.Custom.Base;

namespace IdentityServer.Models.Custom
{
	public class View : CustomBaseEntity
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string TrName { get; set; }
        public string Path { get; set; }
        public int ParentId { get; set; }
        public bool IsMainMenu { get; set; }
        public int UpMenuId { get; set; }
        public string IconName { get; set; }
    }
}


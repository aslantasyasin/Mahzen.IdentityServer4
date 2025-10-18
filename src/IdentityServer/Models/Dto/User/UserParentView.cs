using System;
namespace IdentityServer.Models.Dto.User
{
	public class UserParentView
	{
        public string MenuName { get; set; }
        public string MenuTrName { get; set; }

        public bool IsView { get; set; }
        public bool IsCreate { get; set; }
        public bool IsUpdate { get; set; }
        public bool IsDelete { get; set; }
    }
}


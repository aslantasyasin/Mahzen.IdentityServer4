using System;
using System.Collections.Generic;

namespace IdentityServer.Models.Dto.User
{
	public class UserSubMenuDto
	{
        public string MenuName { get; set; }
        public string MenuTrName { get; set; }
        public string Path { get; set; }
        public string IconName { get; set; }

        public bool IsView { get; set; }
        public bool IsCreate { get; set; }
        public bool IsUpdate { get; set; }
        public bool IsDelete { get; set; }

        public List<UserParentView> ParentViews { get; set; }
    }
}


using System;
using System.Collections.Generic;

namespace IdentityServer.Models.Dto.User
{
	public class UserMenuResponseDto
	{
		public string MenuName { get; set; }
        public string MenuTrName { get; set; }
        public string IconName { get; set; }
        public string Path { get; set; }
        public List<UserSubMenuDto> SubMenus { get; set; }
    }
}


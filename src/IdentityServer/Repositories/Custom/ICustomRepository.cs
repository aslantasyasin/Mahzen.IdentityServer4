using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer.Models.Custom;

namespace IdentityServer.Repositories
{
	public interface ICustomRepository
	{
        Task<List<View>> GetAllViews();
        Task<List<ViewType>> GetAllViewTypes();
    }
}


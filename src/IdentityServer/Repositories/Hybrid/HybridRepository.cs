using System;
using System.Threading.Tasks;
using IdentityServer.Data;
using System.Linq;
using IdentityServer.Models.Dto.User;

namespace IdentityServer.Repositories.Hybrid
{
	public class HybridRepository : IHybridRepository
    {
        private readonly CustomDbContext _customDbContext;
        private readonly ApplicationDbContext _applicationDbContext;

        public HybridRepository(CustomDbContext customDbContext, ApplicationDbContext applicationDbContext)
        {
            _customDbContext = customDbContext;
            _applicationDbContext = applicationDbContext;
        }

        
    }
}


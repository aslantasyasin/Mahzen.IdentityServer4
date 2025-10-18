using System;
using System.Threading.Tasks;
using IdentityServer.Data;
using IdentityServer.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using IdentityServer.Models.Dto.User;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Repositories.Identity
{
    public class IdentityRepository : IIdentityRepository
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public IdentityRepository(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public async Task<List<ApplicationRole>> GetRolesByUserId(string userId)
        {
            var userRoles = await (from ur in _applicationDbContext.UserRoles
                             join r in _applicationDbContext.Roles on ur.RoleId equals r.Id
                             where ur.UserId == userId
                             select r).ToListAsync();
            return userRoles;
        }

        public async Task<List<IdentityRoleClaim<string>>> GetUserRoleClaims(string userId)
        {
            return await (from ur in _applicationDbContext.UserRoles
                          join rc in _applicationDbContext.RoleClaims on ur.RoleId equals rc.RoleId
                          where ur.UserId == userId
                          select rc).ToListAsync();
        }
    }
}


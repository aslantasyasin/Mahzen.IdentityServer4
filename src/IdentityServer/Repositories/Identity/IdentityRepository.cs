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
        
        public async Task<List<UserResponseDto>> GetUsersAsync()
        {
            var users = await (from u in _applicationDbContext.Users
                    join ur in _applicationDbContext.UserRoles on u.Id equals ur.UserId into userRoles
                    from ur in userRoles.DefaultIfEmpty()
                    join r in _applicationDbContext.Roles on ur.RoleId equals r.Id into roles
                    from r in roles.DefaultIfEmpty()
                    group new { u, r } by new 
                    { 
                        u.Id, 
                        u.UserName, 
                        u.FirstName, 
                        u.LastName, 
                        u.Email, 
                        u.PhoneNumber, 
                        u.CreatedDate, 
                        u.IsActive, 
                    } into g
                    select new UserResponseDto
                    {
                        Id = g.Key.Id,
                        UserName = g.Key.UserName,
                        FirstName = g.Key.FirstName,
                        LastName = g.Key.LastName,
                        FullName =g.Key.FirstName + " " + g.Key.LastName,
                        Email = g.Key.Email,
                        PhoneNumber = g.Key.PhoneNumber,
                        CreatedDate = g.Key.CreatedDate,
                        Status = g.Key.IsActive,
                        Role = g.Select(x => x.r.Name)
                            .OrderBy(name => name)
                            .FirstOrDefault()
                    })
                .AsNoTracking()
                .ToListAsync();
            
            return users;
        }
    }
}


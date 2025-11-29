﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Models.Custom;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Repositories
{
    public class CustomRepository : ICustomRepository
    {
        private readonly CustomDbContext _customDbContext;

        public CustomRepository(CustomDbContext customDbContext)
        {
            _customDbContext = customDbContext;
        }
        public async Task<List<View>> GetAllViews()
        {
            return await _customDbContext.View.ToListAsync();
        }

        public async Task<List<ViewType>> GetAllViewTypes()
        {
            return await _customDbContext.ViewType.ToListAsync();
        }
        
        public async Task<List<UserChangeLog>> GetUserChangeLogs(string userId)
        {
            return await _customDbContext.UserChangeLogs
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ChangedAt)
                .ToListAsync();
        }

        public async Task GetUserMenus()
        {

        }
    }
}


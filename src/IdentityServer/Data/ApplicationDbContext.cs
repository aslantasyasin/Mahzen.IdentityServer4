using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Models;
using IdentityServer.Models.Base;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;
using IdentityServer.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Reflection.Metadata;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        private readonly UserInfo _userInfo;
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IServiceScopeFactory serviceScopeFactory)
            : base(options)
        {
            _userInfo  = new UserInfo(serviceScopeFactory);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);

            builder.Entity<ApplicationRole>().HasQueryFilter(x => x.TenantId == _userInfo.TenantId);
            //builder.Entity<ApplicationUser>().HasQueryFilter(x => x.TenantId == _userInfo.TenantId);
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();
            //builder.Entity<ApplicationRoleClaim>().HasQueryFilter(x => x.TenantId == _tenantId);
            //builder.Entity<ApplicationUserClaim>().HasQueryFilter(x => x.TenantId == _tenantId);
            //builder.Entity<ApplicationUserRole>().HasQueryFilter(x => x.TenantId == _tenantId);


        }
    }
}

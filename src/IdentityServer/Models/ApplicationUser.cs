using System.ComponentModel.DataAnnotations;
using IdentityServer.Models.Base;
using IdentityServer.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser, ITenant
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; }
        public UserType UserType { get; set; }
        public int TenantId { get; set; }
    }
}

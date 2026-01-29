using System;
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
        public string CountryCode { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public string CreatedBy { get; set; }
        
    }
}

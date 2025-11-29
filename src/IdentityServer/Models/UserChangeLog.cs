using System;
using IdentityServer.Models.Base;

namespace IdentityServer.Models
{
    public class UserChangeLog : ITenant
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; }
        public int TenantId { get; set; }
    }
}


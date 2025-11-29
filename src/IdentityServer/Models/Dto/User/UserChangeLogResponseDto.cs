using System;
using System.Collections.Generic;

namespace IdentityServer.Models.Dto.User
{
    public class UserChangeLogResponseDto
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; }
    }
}


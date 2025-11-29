﻿using System;
using IdentityServer.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.Dto.User
{
	public class ApplicationUserUpdateRequestDto
	{
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool? IsActive { get; set; }
        public UserType? UserType { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
    }
}


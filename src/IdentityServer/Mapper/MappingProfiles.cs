﻿using System;
using System.Security.Claims;
using AutoMapper;
using IdentityServer.Models;
using IdentityServer.Models.Custom;
using IdentityServer.Models.Dto.Role;
using IdentityServer.Models.Dto.User;

namespace IdentityServer.Mapper
{
	public class MappingProfiles : Profile
	{
		public MappingProfiles()
		{
			CreateMap<ApplicationRole, RoleResponseDto>().ReverseMap();
			CreateMap<Claim, RoleClaimResponseDto>().ReverseMap();
			CreateMap<RoleClaimTypesResponseDto, ViewType>().ReverseMap();
            CreateMap<RoleClaimValuesResponseDto, View>().ReverseMap();
			CreateMap<UserResponseDto, ApplicationUser>().ReverseMap();
            CreateMap<ApplicationUserRequestDto, ApplicationUser>().ReverseMap();
			CreateMap<ApplicationUserUpdateRequestDto, ApplicationUser>().PreserveReferences();
			CreateMap<UserChangeLog, UserChangeLogResponseDto>().ReverseMap();
    //            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
    //            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName != null ? src.UserName : null))
				//.ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.UserName != null ? src.UserName : null))
				//.ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.UserName != null ? src.UserName : null))
    //            .ForMember(dest => dest.UserType, opt => opt.MapFrom(src => src.UserType != null ? src.UserType : null))
    //            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email != null ? src.Email : null));


            //.ReverseMap().ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));
        }
	}
}


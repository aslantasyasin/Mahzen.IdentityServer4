using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Windows.Markup;
using AutoMapper;
using IdentityServer.Models;
using IdentityServer.Models.Base;
using IdentityServer.Models.Dto.Role;
using IdentityServer.Models.Dto.User;
using IdentityServer.Models.Enums;
using IdentityServer.Repositories;
using IdentityServer.Repositories.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUlid;

namespace IdentityServer.Services.User
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserInfo _userInfo;
        private readonly IMapper _mapper;
        private readonly IIdentityRepository _identityRepository;
        private readonly ICustomRepository _customRepository;
        private readonly IUserChangeLogService _userChangeLogService;

        public UserService(UserManager<ApplicationUser> userManager, IServiceScopeFactory serviceScopeFactory, IMapper mapper, IIdentityRepository identityRepository, ICustomRepository customRepository, IUserChangeLogService userChangeLogService)
        {
            _userManager = userManager;
            _userInfo = new UserInfo(serviceScopeFactory);
            _mapper = mapper;
            _identityRepository = identityRepository;
            _customRepository = customRepository;
            _userChangeLogService = userChangeLogService;
        }

        public async Task<ApiResponse<string>> CreateUserAsync(ApplicationUserRequestDto userRequestDto)
        {
            var response = new ApiResponse<string>();
            try
            {
                var getUserByEmail = await _userManager.FindByEmailAsync(userRequestDto.Email);
                if (getUserByEmail != null)
                {
                    var errorMessage = "Bu email ile daha önce kayıt yapılmış!";
                    return ApiResponse<string>.Fail(errorMessage);
                }

                var getUserPhone = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == userRequestDto.PhoneNumber);

                if (getUserPhone != null)
                {
                    var errorMessage = "Bu telefon numarası ile daha önce kayıt yapılmış!";
                    return ApiResponse<string>.Fail(errorMessage);
                }
                
                userRequestDto.Id = Ulid.NewUlid().ToString();
                userRequestDto.UserName = userRequestDto.Email;
                userRequestDto.IsActive = true;
                var map = _mapper.Map<ApplicationUserRequestDto, ApplicationUser>(userRequestDto);
                var addUserResult = await _userManager.CreateAsync(map, userRequestDto.Password);
                if (addUserResult.Succeeded)
                {
                    var claims = new List<Claim>();
                    var tenantClaim = new Claim("tenant_id", map.TenantId.ToString());
                    var userTypeClaim = new Claim("user_type", map.UserType.ToString());

                    claims.Add(tenantClaim);
                    claims.Add(userTypeClaim);

                    var claimresult = await _userManager.AddClaimsAsync(map, claims);

                    if (!claimresult.Succeeded)
                    {
                        return ApiResponse<string>.Fail(claimresult.Errors?.First().Description);
                    }
                }
                else
                {
                    return ApiResponse<string>.Fail(addUserResult.Errors?.First().Description);
                }
                
                return ApiResponse<string>.Success(map.Id);
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.Fail(ex.Message);
            }
        }
        
        public async Task<ApiResponse<bool>> EmailVerified(string userId)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<bool>.Fail("Kullanıcı bulunamadı.");
                }

                user.EmailConfirmed = true;
               
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    response.Data = true;
                }
                else
                {
                    foreach (var err in result.Errors)
                        response.Errors.Add(err.Code);
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> DeleteUserAsync(string userId)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                var deleteUserResult = await _userManager.DeleteAsync(user);

                response.Data = deleteUserResult.Succeeded;

            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<UserResponseDto>>> GetUsersAsync()
        {
            var response = new ApiResponse<List<UserResponseDto>>();
            try
            {
                var users = await _userManager.Users.Where(x => x.TenantId == _userInfo.TenantId).ToListAsync();

                response.Data = _mapper.Map<List<ApplicationUser>, List<UserResponseDto>>(users);

            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }
        
        public async Task<ApiResponse<UserResponseDto>> GetUserByIdAsync(string id)
        {
            var response = new ApiResponse<UserResponseDto>();
            try
            {
                var user = await _userManager.FindByIdAsync(id);

                response.Data = _mapper.Map<UserResponseDto>(user);

            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> UserIsActiveControlAsync(string userName)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByNameAsync(userName);

                if (user == null)
                    response.Errors.Add("Böyle bir kullanıcı bulunamadı!");
                else if (!user.IsActive)
                    response.Errors.Add("Kullanıcı akif değil! Yöneticinize başvurun.");
                else
                    response.Data = true;
                
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> UpdateUserAsync(string userId, ApplicationUserUpdateRequestDto userRequestDto)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<bool>.Fail("Kullanıcı bulunamadı.");
                }

                // Değişiklikleri logla
                await LogUserChangesAsync(user, userRequestDto, _userInfo.UserId);
                
                UserMap(userRequestDto, user);
                var updateUserResult = await _userManager.UpdateAsync(user);

                response.Data = updateUserResult.Succeeded;

            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<ApplicationRole>>> GetRoleByUserIdAsync(string userId)
        {
            var response = new ApiResponse<List<ApplicationRole>>();
            try
            {
                response.Data = await _identityRepository.GetRolesByUserId(userId);
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> AddUserRoleAsync(UserRoleRequestDto addUserRoleRequestDto)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(addUserRoleRequestDto.UserId);
                var result = await _userManager.AddToRoleAsync(user, addUserRoleRequestDto.RoleName);

                response.Data = result.Succeeded;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> DeleteUserRoleAsync(UserRoleRequestDto addUserRoleRequestDto)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(addUserRoleRequestDto.UserId);
                var result = await _userManager.RemoveFromRoleAsync(user, addUserRoleRequestDto.RoleName);

                response.Data = result.Succeeded;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<List<UserMenuResponseDto>>> GetUserMenusAsync(string userId)
        {
            var response = new ApiResponse<List<UserMenuResponseDto>>();
            try
            {
                var roleClaims = await _identityRepository.GetUserRoleClaims(userId);

                var views = await _customRepository.GetAllViews();

                var userMenus = new List<UserMenuResponseDto>();
                
                foreach (var main in views.Where(x=> x.IsMainMenu).ToList())
                {
                    var mainMenu = new UserMenuResponseDto();

                    mainMenu.MenuName = main.Name;
                    mainMenu.MenuTrName = main.TrName;
                    mainMenu.IconName = main.IconName;
                    mainMenu.Path = main.Path;

                    mainMenu.SubMenus = new List<UserSubMenuDto>();

                    foreach (var sub in views.Where(x=> x.UpMenuId == main.Id).ToList())
                    {
                        if(roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.View.ToString()))
                        {
                            var subMenu = new UserSubMenuDto();

                            subMenu.MenuName = sub.Name;
                            subMenu.MenuTrName = sub.TrName;
                            subMenu.IconName = sub.IconName;
                            subMenu.Path = sub.Path;

                            subMenu.IsView = true;
                            subMenu.IsCreate = roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.Create.ToString());
                            subMenu.IsUpdate = roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.Update.ToString());
                            subMenu.IsDelete = roleClaims.Any(x => x.ClaimValue == sub.Name && x.ClaimType == ActionType.Delete.ToString());

                            subMenu.ParentViews = new List<UserParentView>();

                            foreach (var parent in views.Where(x => x.ParentId == sub.Id).ToList())
                            {
                                var parentView = new UserParentView();

                                parentView.MenuName = parent.Name;
                                parentView.MenuTrName = parent.TrName;

                                parentView.IsView = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.View.ToString());
                                parentView.IsCreate = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.Create.ToString());
                                parentView.IsUpdate = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.Update.ToString());
                                parentView.IsDelete = roleClaims.Any(x => x.ClaimValue == parent.Name && x.ClaimType == ActionType.Delete.ToString());

                                subMenu.ParentViews.Add(parentView);
                            }
                            mainMenu.SubMenus.Add(subMenu);
                        }
                        
                    }
                    if(mainMenu.SubMenus.Any() || !string.IsNullOrEmpty(mainMenu.Path))
                        userMenus.Add(mainMenu);

                }
                response.Data = userMenus;
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }

        public async Task<ApiResponse<bool>> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<bool>.Fail("Kullanıcı bulunamadı.");
                }
                
                if (string.IsNullOrEmpty(currentPassword))
                {
                    return ApiResponse<bool>.Fail("Mevcut şifreyi giriniz.");
                }

                IdentityResult result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

                if (result.Succeeded)
                {
                    // Şifre değişikliğini logla (şifreleri kaydetme, sadece bilgi)
                    await _userChangeLogService.LogChangeAsync(userId, "Password", "***", "***", _userInfo.UserId);
                    response.Data = true;
                }
                else
                {
                    foreach (var err in result.Errors)
                        response.Errors.Add(err.Code);
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }
        
        public async Task<ApiResponse<UserContactResponseDto>> GetContactInfoByUserId(string userId)
        {
            var response = new ApiResponse<UserContactResponseDto>();
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<UserContactResponseDto>.Fail("Kullanıcı bulunamadı.");
                }
                
                response.Data = new UserContactResponseDto
                {
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber
                };
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            
            return response;
        }
        
        public async Task<ApiResponse<List<UserChangeLogResponseDto>>> GetUserChangeLogsAsync(string userId)
        {
            var response = new ApiResponse<List<UserChangeLogResponseDto>>();
            try
            {
                var logs = await _customRepository.GetUserChangeLogs(userId);
                response.Data = _mapper.Map<List<UserChangeLogResponseDto>>(logs);
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
            }
            return response;
        }
        
        public async Task<ApiResponse<bool>> UpdateEmailAsync(UpdateEmailRequestDto model)
        {
            var response = new ApiResponse<bool>();
            
            // Token'dan kullanıcı id kontrolü
            var userId = _userInfo.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return ApiResponse<bool>.Fail("Token'dan kullanıcı id alınamadı.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ApiResponse<bool>.Fail("Kullanıcı bulunamadı.");
            }

            // Email zaten kullanılıyor mu kontrol et
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null && existingUser.Id != userId)
            {
                return ApiResponse<bool>.Fail("Bu email adresi başka bir kullanıcı tarafından kullanılmaktadır.");
            }

            // Eski değerleri sakla (loglama için)
            var oldEmail = user.Email;
            var oldUserName = user.UserName;
            var isUsernameChanged = user.UserName == oldEmail;

            // TransactionScope ile atomik işlem
            using (var scope = new System.Transactions.TransactionScope(
                System.Transactions.TransactionScopeOption.Required,
                new System.Transactions.TransactionOptions 
                { 
                    IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted 
                },
                System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // 1. Email güncelle
                    var token = await _userManager.GenerateChangeEmailTokenAsync(user, model.Email);
                    var emailChangeResult = await _userManager.ChangeEmailAsync(user, model.Email, token);

                    if (!emailChangeResult.Succeeded)
                    {
                        foreach (var err in emailChangeResult.Errors)
                            response.Errors.Add(err.Description);
                        return response;
                    }

                    // 2. UserName'i de email ile senkronize et (eğer gerekirse)
                    if (isUsernameChanged)
                    {
                        user.UserName = model.Email;
                        var usernameUpdateResult = await _userManager.UpdateAsync(user);
                        
                        if (!usernameUpdateResult.Succeeded)
                        {
                            response.Errors.Add("UserName güncellenirken hata oluştu.");
                            foreach (var err in usernameUpdateResult.Errors)
                                response.Errors.Add(err.Description);
                            // Transaction scope dispose olacak, otomatik rollback
                            return response;
                        }
                    }

                    // 3. Email doğrulamasını sıfırla
                    user.EmailConfirmed = false;
                    var emailConfirmUpdateResult = await _userManager.UpdateAsync(user);

                    if (!emailConfirmUpdateResult.Succeeded)
                    {
                        response.Errors.Add("Email doğrulaması güncellenirken hata oluştu.");
                        foreach (var err in emailConfirmUpdateResult.Errors)
                            response.Errors.Add(err.Description);
                        // Transaction scope dispose olacak, otomatik rollback
                        return response;
                    }

                    // 4. Değişiklikleri logla
                    await _userChangeLogService.LogChangeAsync(userId, "Email", oldEmail, model.Email, userId);
                    
                    if (isUsernameChanged)
                    {
                        await _userChangeLogService.LogChangeAsync(userId, "UserName", oldUserName, model.Email, userId);
                    }

                    // Tüm işlemler başarılı, transaction'ı commit et
                    scope.Complete();
                    response.Data = true;
                }
                catch (Exception ex)
                {
                    // Transaction otomatik rollback olacak
                    response.Errors.Add($"Email güncelleme sırasında hata oluştu: {ex.Message}");
                }
            }
            
            return response;
        }
        
        public async Task<ApiResponse<bool>> UpdateProfileInfoAsync(UpdateProfileInfoRequestDto model)
        {
            var response = new ApiResponse<bool>();
            
            // Token'dan kullanıcı id kontrolü
            var userId = _userInfo.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return ApiResponse<bool>.Fail("Token'dan kullanıcı id alınamadı.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ApiResponse<bool>.Fail("Kullanıcı bulunamadı.");
            }

            // Telefon numarası değişiyorsa ve başka kullanıcıda varsa kontrol et
            if (!string.IsNullOrEmpty(model.PhoneNumber) && model.PhoneNumber != user.PhoneNumber)
            {
                var existingUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber && u.Id != userId);
                
                if (existingUser != null)
                {
                    return ApiResponse<bool>.Fail("Bu telefon numarası başka bir kullanıcı tarafından kullanılmaktadır.");
                }
            }

            // Eski değerleri sakla
            var oldFirstName = user.FirstName;
            var oldLastName = user.LastName;
            var oldPhoneNumber = user.PhoneNumber;
            
            // Değişiklik var mı kontrol et
            var hasChanges = false;
            if (!string.IsNullOrEmpty(model.FirstName) && model.FirstName != oldFirstName) hasChanges = true;
            if (!string.IsNullOrEmpty(model.LastName) && model.LastName != oldLastName) hasChanges = true;
            if (!string.IsNullOrEmpty(model.PhoneNumber) && model.PhoneNumber != oldPhoneNumber) hasChanges = true;

            if (!hasChanges)
            {
                response.Data = true;
                return response;
            }

            // TransactionScope ile atomik işlem
            using (var scope = new System.Transactions.TransactionScope(
                System.Transactions.TransactionScopeOption.Required,
                new System.Transactions.TransactionOptions 
                { 
                    IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted 
                },
                System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    // 1. FirstName güncelle
                    if (!string.IsNullOrEmpty(model.FirstName) && model.FirstName != oldFirstName)
                    {
                        user.FirstName = model.FirstName;
                    }

                    // 2. LastName güncelle
                    if (!string.IsNullOrEmpty(model.LastName) && model.LastName != oldLastName)
                    {
                        user.LastName = model.LastName;
                    }

                    // 3. PhoneNumber güncelle
                    var phoneNumberChanged = false;
                    if (!string.IsNullOrEmpty(model.PhoneNumber) && model.PhoneNumber != oldPhoneNumber)
                    {
                        user.PhoneNumber = model.PhoneNumber;
                        user.PhoneNumberConfirmed = false;
                        phoneNumberChanged = true;
                    }

                    // 4. Kullanıcıyı güncelle
                    var updateResult = await _userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        foreach (var err in updateResult.Errors)
                            response.Errors.Add(err.Description);
                        return response;
                    }

                    // 5. Değişiklikleri logla
                    if (!string.IsNullOrEmpty(model.FirstName) && model.FirstName != oldFirstName)
                    {
                        await _userChangeLogService.LogChangeAsync(userId, "FirstName", oldFirstName, model.FirstName, userId);
                    }

                    if (!string.IsNullOrEmpty(model.LastName) && model.LastName != oldLastName)
                    {
                        await _userChangeLogService.LogChangeAsync(userId, "LastName", oldLastName, model.LastName, userId);
                    }

                    if (phoneNumberChanged)
                    {
                        await _userChangeLogService.LogChangeAsync(userId, "PhoneNumber", oldPhoneNumber, model.PhoneNumber, userId);
                    }

                    // Tüm işlemler başarılı, transaction'ı commit et
                    scope.Complete();
                    response.Data = true;
                }
                catch (Exception ex)
                {
                    // Transaction otomatik rollback olacak
                    response.Errors.Add($"Profil bilgileri güncelleme sırasında hata oluştu: {ex.Message}");
                }
            }
            
            return response;
        }

        private async Task LogUserChangesAsync(ApplicationUser user, ApplicationUserUpdateRequestDto userRequestDto, string changedBy)
        {
            if (userRequestDto.IsActive != null && user.IsActive != userRequestDto.IsActive)
            {
                await _userChangeLogService.LogChangeAsync(user.Id, "IsActive", user.IsActive.ToString(), userRequestDto.IsActive.ToString(), changedBy);
            }

            if (userRequestDto.UserName != null && user.UserName != userRequestDto.UserName)
            {
                await _userChangeLogService.LogChangeAsync(user.Id, "UserName", user.UserName, userRequestDto.UserName, changedBy);
            }

            if (userRequestDto.FirstName != null && user.FirstName != userRequestDto.FirstName)
            {
                await _userChangeLogService.LogChangeAsync(user.Id, "FirstName", user.FirstName, userRequestDto.FirstName, changedBy);
            }

            if (userRequestDto.LastName != null && user.LastName != userRequestDto.LastName)
            {
                await _userChangeLogService.LogChangeAsync(user.Id, "LastName", user.LastName, userRequestDto.LastName, changedBy);
            }

            if (userRequestDto.UserType != null && user.UserType != userRequestDto.UserType)
            {
                await _userChangeLogService.LogChangeAsync(user.Id, "UserType", user.UserType.ToString(), userRequestDto.UserType.ToString(), changedBy);
            }

            if (userRequestDto.Email != null && user.Email != userRequestDto.Email)
            {
                await _userChangeLogService.LogChangeAsync(user.Id, "Email", user.Email, userRequestDto.Email, changedBy);
            }

            if (userRequestDto.PhoneNumber != null && user.PhoneNumber != userRequestDto.PhoneNumber)
            {
                await _userChangeLogService.LogChangeAsync(user.Id, "PhoneNumber", user.PhoneNumber, userRequestDto.PhoneNumber, changedBy);
            }
        }

        private void UserMap(ApplicationUserUpdateRequestDto userRequestDto, ApplicationUser user)
        {
            if (userRequestDto.IsActive != null)
                user.IsActive = userRequestDto.IsActive ?? false;
            if (userRequestDto.UserName != null)
                user.UserName = userRequestDto.UserName;
            if (userRequestDto.FirstName != null)
                user.FirstName = userRequestDto.FirstName;
            if (userRequestDto.LastName != null)
                user.LastName = userRequestDto.LastName;
            if (userRequestDto.UserType != null)
                user.UserType = userRequestDto.UserType ?? UserType.Default;
            if (userRequestDto.Email != null)
                user.Email = userRequestDto.Email;
            if (userRequestDto.PhoneNumber != null)
                user.PhoneNumber = userRequestDto.PhoneNumber;
        }
    }
}

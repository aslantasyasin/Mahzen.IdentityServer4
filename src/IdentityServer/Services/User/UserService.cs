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
using Microsoft.Extensions.Logging;
using NUlid;

namespace IdentityServer.Services.User
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserInfo _userInfo;
        private readonly IMapper _mapper;
        private readonly IIdentityRepository _identityRepository;
        private readonly ICustomRepository _customRepository;
        private readonly IUserChangeLogService _userChangeLogService;
        private readonly IUserConsentService _userConsentService;
        private readonly ILogger<UserService> _logger;

        // B2C kaydında alınması zorunlu onaylar. Tek otorite burasıdır; yeni bir
        // zorunlu doküman eklenirse yalnızca bu dizi güncellenir.
        private static readonly ConsentDocumentType[] RequiredRegistrationConsents =
        {
            ConsentDocumentType.TermsOfUse,
            ConsentDocumentType.PrivacyPolicy
        };

        public UserService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IServiceScopeFactory serviceScopeFactory, IMapper mapper, IIdentityRepository identityRepository, ICustomRepository customRepository, IUserChangeLogService userChangeLogService, IUserConsentService userConsentService, ILogger<UserService> logger)
        {
            _userManager = userManager;
            _userInfo = new UserInfo(serviceScopeFactory);
            _mapper = mapper;
            _identityRepository = identityRepository;
            _customRepository = customRepository;
            _userChangeLogService = userChangeLogService;
            _userConsentService = userConsentService;
            _logger = logger;
            _roleManager = roleManager;
        }

        public const string ConsentRequiredMessage = "Üyelik Sözleşmesi ve Gizlilik Politikası onayı zorunludur.";

        // SHA-256 hex özeti uzunluğu. Onay kaydındaki ContentHash bu biçimde olmalı.
        private const int ContentHashLength = 64;

        // Kabul edilebilir onay dizisi üst sınırı. Zorunlu doküman sayısı bugün 2;
        // sınır, ileride doküman eklenirse kodu değiştirmeye gerek kalmasın diye
        // biraz yüksek tutuldu. NEDEN gerekli: gövde deserialize edildikten sonra
        // dizi üzerinde tarama yapılıyor ve Kestrel'in gövde boyutu sınırı (30MB)
        // yüz binlerce elemana izin verir; sınır, taramayı en baştan kesiyor.
        private const int MaxAcceptedDocuments = 10;

        // Yazılacak onay listesini kullanıcı OLUŞTURULMADAN ÖNCE kurar; geçersizse null döner.
        // NEDEN önce: eksik onay burada yakalanırsa hiçbir yan etki oluşmaz, telafi gerekmez.
        //
        // NEDEN gelen liste olduğu gibi kullanılmıyor da ZORUNLU liste üzerinden yeniden
        // kuruluyor: Register ucu AllowAnonymous ve gateway üzerinden dışarı açık. Gelen
        // diziyi olduğu gibi yazmak, kimliksiz bir çağıranın tek istekte on binlerce satır
        // ekletmesine izin verirdi (md. 5 — girdi kümesi sınırsız büyüyemez). Bu kurguda
        // girdi kaç eleman içerirse içersin en fazla RequiredRegistrationConsents.Length
        // satır yazılır ve tekrar eden türler kendiliğinden elenir.
        private static List<ConsentAcceptanceDto> ResolveRegistrationConsents(List<ConsentAcceptanceDto> documents)
        {
            if (documents == null || documents.Count == 0 || documents.Count > MaxAcceptedDocuments)
                return null;

            var resolved = new List<ConsentAcceptanceDto>(RequiredRegistrationConsents.Length);

            foreach (var required in RequiredRegistrationConsents)
            {
                // NEDEN Enum.TryParse ile değil metin karşılaştırmasıyla: TryParse tanımsız
                // sayısal değerleri de başarıyla ayrıştırır — "999" true döner ve enum'a
                // aralık dışı bir değer olarak yazılır. Beklenen adın birebir eşleşmesi
                // aranarak bu yol kapatılıyor.
                var requiredName = required.ToString();
                var match = documents.FirstOrDefault(d =>
                    string.Equals(d?.DocumentType, requiredName, StringComparison.Ordinal));

                if (match == null)
                    return null;

                // Sürüm ve içerik özeti olmayan onay kaydı ispat değeri taşımaz;
                // eksik/bozuk veriyle yazmaktansa kaydı reddetmek doğru.
                if (string.IsNullOrWhiteSpace(match.Version) || !IsSha256Hex(match.ContentHash))
                    return null;

                resolved.Add(match);
            }

            return resolved;
        }

        private static bool IsSha256Hex(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != ContentHashLength)
                return false;

            foreach (var c in value)
            {
                var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }

            return true;
        }

        public async Task<ApiResponse<string>> CreateUserAsync(ApplicationUserRequestDto userRequestDto)
        {
            var response = new ApiResponse<string>();
            try
            {
                var consentsToRecord = ResolveRegistrationConsents(userRequestDto.AcceptedDocuments);
                if (consentsToRecord == null)
                    return ApiResponse<string>.Fail(ConsentRequiredMessage);

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
                
                
                var userMap = _mapper.Map<ApplicationUserRequestDto, ApplicationUser>(userRequestDto);
                
                userMap.Id = Ulid.NewUlid().ToString();
                userMap.UserName = userRequestDto.Email;
                userMap.IsActive = true;
                userMap.CreatedDate = DateTime.UtcNow;
                userMap.CreatedBy = _userInfo.UserId ?? "System";
                
                var addUserResult = await _userManager.CreateAsync(userMap, userRequestDto.Password);
                if (addUserResult.Succeeded)
                {
                    var claims = new List<Claim>();
                    var tenantClaim = new Claim("tenant_id", userMap.TenantId.ToString());
                    var userTypeClaim = new Claim("user_type", userMap.UserType.ToString());

                    claims.Add(tenantClaim);
                    claims.Add(userTypeClaim);

                    var claimresult = await _userManager.AddClaimsAsync(userMap, claims);

                    if (!claimresult.Succeeded)
                    {
                        return ApiResponse<string>.Fail(claimresult.Errors?.First().Description);
                    }

                    // Kullanıcıya role atama
                    var addToRoleResult = await _userManager.AddToRoleAsync(userMap, nameof(Roles.Customer));
                    if (!addToRoleResult.Succeeded)
                    {
                        return ApiResponse<string>.Fail(addToRoleResult.Errors?.First().Description);
                    }

                    // Onay kaydı. TenantId parametre olarak geçiliyor: kayıt endpoint'i
                    // AllowAnonymous, Authorization header yok, bu yüzden _userInfo.TenantId
                    // burada 0 döner. Doğru değer yeni oluşturulan kullanıcının kendisindedir.
                    try
                    {
                        await _userConsentService.RecordAsync(
                            userMap.Id,
                            userMap.TenantId,
                            consentsToRecord,
                            UserConsent.SourceRegister);
                    }
                    catch (Exception consentEx)
                    {
                        // Telafi edici geri alma: CustomDbContext ile Identity aynı
                        // transaction'da değil. Onay kaydı yazılamadıysa ispatı olmayan bir
                        // kullanıcı geride kalmamalı — kullanıcı saniyeler önce yaratıldı,
                        // siparişi/ilişkisi yok, silmek güvenli.
                        // Log'a e-posta YAZILMIYOR: PII, ve Serilog Graylog'a gönderiyor.
                        // Elle onarım için UserId yeterli (md. 9 + md. 13).
                        _logger.LogError(consentEx,
                            "UserConsent yazılamadı, kayıt geri alınıyor. UserId={UserId} TenantId={TenantId}",
                            userMap.Id, userMap.TenantId);

                        var rollbackResult = await _userManager.DeleteAsync(userMap);
                        if (!rollbackResult.Succeeded)
                        {
                            // Elle müdahale gerekir: onaysız kullanıcı DB'de kaldı.
                            _logger.LogError(
                                "UserConsent geri alması BASARISIZ. Onaysız kullanıcı silinemedi, elle silinmeli. UserId={UserId} Hata={Errors}",
                                userMap.Id,
                                string.Join(" | ", rollbackResult.Errors.Select(e => e.Description)));
                        }

                        return ApiResponse<string>.Fail("Kayıt tamamlanamadı, lütfen tekrar deneyin.");
                    }
                }
                else
                {
                    return ApiResponse<string>.Fail(addUserResult.Errors?.First().Description);
                }
                
                return ApiResponse<string>.Success(userMap.Id);
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.Fail(ex.Message);
            }
        }
        
        public async Task<ApiResponse<string>> CreateUserByB2bAsync(ApplicationUserRequestDto userRequestDto, string roleName)
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
                
                
                var userMap = _mapper.Map<ApplicationUserRequestDto, ApplicationUser>(userRequestDto);
                
                userMap.Id = Ulid.NewUlid().ToString();
                userMap.UserName = userRequestDto.Email;
                userMap.IsActive = userRequestDto.IsActive;
                userMap.CreatedDate = DateTime.UtcNow;
                userMap.CreatedBy = _userInfo.UserId ?? "System";
                
                var addUserResult = await _userManager.CreateAsync(userMap, userRequestDto.Password);
                if (addUserResult.Succeeded)
                {
                    var claims = new List<Claim>();
                    var tenantClaim = new Claim("tenant_id", userMap.TenantId.ToString());
                    var userTypeClaim = new Claim("user_type", userMap.UserType.ToString());

                    claims.Add(tenantClaim);
                    claims.Add(userTypeClaim);

                    var claimresult = await _userManager.AddClaimsAsync(userMap, claims);

                    if (!claimresult.Succeeded)
                    {
                        return ApiResponse<string>.Fail(claimresult.Errors?.First().Description);
                    }
                    
                    roleName = !string.IsNullOrEmpty(roleName) ? roleName : nameof(Roles.Supplier);

                    // Rol kontrolü
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        return ApiResponse<string>.Fail("Rol bulunamadı!");
                    }

                    // Kullanıcıya role atama
                    var addToRoleResult = await _userManager.AddToRoleAsync(userMap, roleName);
                    if (!addToRoleResult.Succeeded)
                    {
                        return ApiResponse<string>.Fail(addToRoleResult.Errors?.First().Description);
                    }
                    
                }
                else
                {
                    return ApiResponse<string>.Fail(addUserResult.Errors?.First().Description);
                }
                
                return ApiResponse<string>.Success(userMap.Id);
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
                response.Data = await _identityRepository.GetUsersAsync();;
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

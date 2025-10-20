using IdentityModel.Client;
using IdentityServer.Models.Dto.User;
using IdentityServer.Services.User;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using NUlid;

namespace IdentityServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }
        
        [HttpPost]
        public async Task<IActionResult> SignUp()
        {
            var client = new HttpClient();

            var disco = await client.GetDiscoveryDocumentAsync("https://localhost:5001");

            var password = new PasswordTokenRequest
            {
                Address = disco.TokenEndpoint,
                UserName = "fcakiroglu@outlook.com",
                Password = "password",
                ClientId = "client2"
            };

            var token = await client.RequestPasswordTokenAsync(password);

            // Dönen token'ı debug veya frontend'e göstermek için dönüyoruz. Üretimde AccessToken'ı doğrudan döndürme.
            return Ok(token);
        }

        //[HttpPost]
        //public async Task<IActionResult> Register([FromBody]ApplicationUserRequestDto appUser)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //        {
        //            return BadRequest(ModelState);
        //        }
        //        var registerResult = await _userService.UserRegister(appUser);

        //        return Ok(registerResult);
        //    }
        //    catch (System.Exception ex)
        //    {

        //    }


        //    return Ok("Register up çalıştı");
        //}

        //[HttpGet]
        //public async Task<IActionResult> GetUserInfo()
        //{

        // var res = await _userManager.get
        //return Ok("Register up çalıştı");
        //}

        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsers()
        {
            var result = await _userService.GetUsersAsync();

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] ApplicationUserRequestDto userRequestDto)
        {
            // Not: Controller'da [ApiController] varsa framework otomatik olarak model doğrulama hatalarında 400 döndürür.
            // Eğer özel bir hata formatı istiyorsanız Program.cs içinde ApiBehaviorOptions.SuppressModelStateInvalidFilter = true yapıp burada ModelState'i kontrol edebilirsiniz.
            

            var result = await _userService.CreateUserAsync(userRequestDto);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpPut("UpdateUser/{userId}")]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] ApplicationUserUpdateRequestDto userRequestDto)
        {
            var result = await _userService.UpdateUserAsync(userId, userRequestDto);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpDelete("DeleteUser/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var result = await _userService.DeleteUserAsync(userId);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpGet("UserIsActiveControl")]
        public async Task<IActionResult> UserIsActiveControl(string userName)
        {
            var result = await _userService.UserIsActiveControlAsync(userName);
            
            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpGet("GetRolesByUserId")]
        public async Task<IActionResult> GetRolesByUserId(string userId)
        {
            var result = await _userService.GetRoleByUserIdAsync(userId);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpPost("AddUserRole")]
        public async Task<IActionResult> AddUserRole(UserRoleRequestDto addUserRoleRequest)
        {
            var result = await _userService.AddUserRoleAsync(addUserRoleRequest);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpPost("DeleteUserRole")]
        public async Task<IActionResult> DeleteUserRole(UserRoleRequestDto addUserRoleRequest)
        {
            var result = await _userService.DeleteUserRoleAsync(addUserRoleRequest);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }

        [HttpGet("GetUserMenus")]
        public async Task<IActionResult> GetUserMenus(string userId)
        {
            var result = await _userService.GetUserMenusAsync(userId);

            if (result.HasError)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }
    }
}

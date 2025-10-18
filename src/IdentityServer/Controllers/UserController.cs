using IdentityModel.Client;
using IdentityServer.Models;
using IdentityServer.Models.Dto.User;
using IdentityServer.Services.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using static IdentityServer4.IdentityServerConstants;

namespace IdentityServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(LocalApi.PolicyName)]
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

            var password = new PasswordTokenRequest();

            password.Address = disco.TokenEndpoint;
            password.UserName = "fcakiroglu@outlook.com";
            password.Password = "password";
            password.ClientId = "client2";

            var token = await client.RequestPasswordTokenAsync(password);

            var aa = new UserInfoRequest();
            aa.Token = "";

            return Ok("sign up çalıştı");
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

        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUser(ApplicationUserRequestDto userRequestDto)
        {
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

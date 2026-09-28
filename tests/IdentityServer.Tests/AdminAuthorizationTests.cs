using System.Reflection;
using System.Security.Claims;
using IdentityServer;
using IdentityServer.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace IdentityServer.Tests;

public class AdminAuthorizationTests
{
    private const string AdminPolicy = "RequireAdminRole";

    private static readonly string[] AdminOnlyUserActions =
    {
        nameof(UserController.GetUserById),
        nameof(UserController.UpdateUser),
        nameof(UserController.DeleteUser),
        nameof(UserController.UserIsActiveControl),
        nameof(UserController.GetRolesByUserId),
        nameof(UserController.AddUserRole),
        nameof(UserController.DeleteUserRole),
        nameof(UserController.GetUserMenus),
    };

    private static readonly string[] SelfServiceUserActions =
    {
        nameof(UserController.ChangePassword),
        nameof(UserController.UpdateProfileInfo),
        nameof(UserController.UpdateEmail),
        nameof(UserController.GetUserChangeLogs),
    };

    private static bool RequiresAdmin(MethodInfo action) =>
        action.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>())
            .Any(a => a.Policy == AdminPolicy);

    private static MethodInfo Action(Type controller, string name) =>
        controller.GetMethod(name) ?? throw new InvalidOperationException($"{controller.Name}.{name} bulunamadı");

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    public static IEnumerable<object[]> AdminOnlyUserActionData => AdminOnlyUserActions.Select(a => new object[] { a });
    public static IEnumerable<object[]> SelfServiceUserActionData => SelfServiceUserActions.Select(a => new object[] { a });

    [Theory]
    [MemberData(nameof(AdminOnlyUserActionData))]
    public void User_actions_touching_other_accounts_require_admin(string action) =>
        Assert.True(RequiresAdmin(Action(typeof(UserController), action)), $"{action} admin politikası istemiyor");

    [Theory]
    [MemberData(nameof(SelfServiceUserActionData))]
    public void Self_service_user_actions_stay_open_to_customers(string action) =>
        Assert.False(RequiresAdmin(Action(typeof(UserController), action)), $"{action} müşteriye kapanmış");

    [Fact]
    public void Every_role_action_requires_admin()
    {
        var actions = Actions(typeof(RoleController)).ToList();
        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.True(RequiresAdmin(a), $"RoleController.{a.Name} admin politikası istemiyor"));
    }

    [Fact]
    public void Every_user_action_is_classified()
    {
        var known = AdminOnlyUserActions.Concat(SelfServiceUserActions).ToHashSet();
        var otherwiseGuarded = Actions(typeof(UserController))
            .Where(a => a.GetCustomAttributes<AuthorizeAttribute>().Any() || a.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(a => a.Name);
        var unclassified = Actions(typeof(UserController)).Select(a => a.Name).Except(known).Except(otherwiseGuarded);
        Assert.Empty(unclassified);
    }

    private static ClaimsPrincipal UserWith(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    [Theory]
    [InlineData("role", "Admin")]
    [InlineData("role", "admin")]
    [InlineData(ClaimTypes.Role, "Admin")]
    public void Admin_role_claim_is_recognised(string type, string value) =>
        Assert.True(Startup.HasAdminRole(UserWith(new Claim(type, value))));

    [Fact]
    public void Admin_is_recognised_when_it_is_not_the_first_role() =>
        Assert.True(Startup.HasAdminRole(UserWith(new Claim("role", "Customer"), new Claim("role", "Admin"))));

    [Fact]
    public void Non_admin_roles_are_rejected() =>
        Assert.False(Startup.HasAdminRole(UserWith(new Claim("role", "Customer"), new Claim("role", "B2B"))));

    [Fact]
    public void User_without_roles_is_rejected() =>
        Assert.False(Startup.HasAdminRole(UserWith(new Claim("sub", "u1"))));

    [Fact]
    public void Admin_value_in_another_claim_type_is_rejected() =>
        Assert.False(Startup.HasAdminRole(UserWith(new Claim("user_type", "Admin"))));
}

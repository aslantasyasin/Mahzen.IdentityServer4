using System.Reflection;
using IdentityServer.Models;
using IdentityServer.Models.Base;
using IdentityServer.Models.Dto.Role;
using IdentityServer.Services.Role;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer.Tests;

public class RoleServiceErrorTests
{
    private const string InternalDetail = "Npgsql: relation \"ids4.AspNetRoles\" does not exist";

    public class FailingStore : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException(InternalDetail);
    }

    [Fact]
    public async Task A_failing_role_store_is_logged_and_hidden()
    {
        var store = DispatchProxy.Create<IRoleStore<ApplicationRole>, FailingStore>();
        var roles = new RoleManager<ApplicationRole>(store, Array.Empty<IRoleValidator<ApplicationRole>>(), null!, new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);
        var scopes = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() })
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var log = new PasswordProtectedChangesTests.RecordingLogger<RoleService>();

        var result = await new RoleService(roles, null!, null!, scopes, log).CreateRoleAsync(new RoleRequestDto { Name = "Satici" });

        Assert.Equal(new[] { ApiErrors.Unexpected }, result.Errors);
        Assert.Contains(log.Errors, ex => ex?.Message == InternalDetail);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Tests;

public class UserInfoTests
{
    private static UserInfo Create(HttpContext? context)
    {
        var scopes = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context })
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        return new UserInfo(scopes);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void Values_come_from_the_authenticated_user()
    {
        var context = new DefaultHttpContext { User = Authenticated(new Claim("sub", "user-1"), new Claim("tenantid", "7")) };

        var info = Create(context);

        Assert.Equal("user-1", info.UserId);
        Assert.Equal(7, info.TenantId);
    }

    [Fact]
    public void Values_are_read_when_used_so_authentication_after_construction_counts()
    {
        var context = new DefaultHttpContext();
        var info = Create(context);

        context.User = Authenticated(new Claim("sub", "user-1"));

        Assert.Equal("user-1", info.UserId);
    }

    [Fact]
    public void Without_a_user_there_is_no_user_id_and_tenant_is_zero()
    {
        var info = Create(new DefaultHttpContext());

        Assert.Null(info.UserId);
        Assert.Equal(0, info.TenantId);
    }

    [Fact]
    public void Without_an_http_context_there_is_no_user_id_and_tenant_is_zero()
    {
        var info = Create(null);

        Assert.Null(info.UserId);
        Assert.Equal(0, info.TenantId);
    }

    [Theory]
    [InlineData("Bearer x.y.z")]
    [InlineData("Bearer abc")]
    [InlineData("Bearer")]
    [InlineData("Basic Y2xpZW50OnNlY3JldA==")]
    public void A_malformed_or_non_bearer_header_does_not_throw(string header)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = header;

        var info = Create(context);

        Assert.Null(info.UserId);
        Assert.Equal(0, info.TenantId);
    }

    [Fact]
    public void An_unsigned_token_in_the_header_is_not_trusted()
    {
        var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: new[] { new Claim("sub", "victim"), new Claim("tenantid", "1") }));
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = $"Bearer {forged}";

        var info = Create(context);

        Assert.Null(info.UserId);
        Assert.Equal(0, info.TenantId);
    }

    [Fact]
    public void A_non_numeric_tenant_claim_is_zero()
    {
        var context = new DefaultHttpContext { User = Authenticated(new Claim("sub", "user-1"), new Claim("tenantid", "abc")) };

        Assert.Equal(0, Create(context).TenantId);
    }
}

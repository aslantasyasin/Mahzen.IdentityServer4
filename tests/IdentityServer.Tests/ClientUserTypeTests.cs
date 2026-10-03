using IdentityServer.Validators;
using IdentityServer4.Models;

namespace IdentityServer.Tests;

public class ClientUserTypeTests
{
    private static Client ClientNamed(string clientId, string? allowedUserTypes = null) => new()
    {
        ClientId = clientId,
        Properties = allowedUserTypes is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [CustomResourceOwnerPasswordValidator.AllowedUserTypesProperty] = allowedUserTypes },
    };

    private static bool Allowed(Client client, params string[] userTypes) =>
        CustomResourceOwnerPasswordValidator.IsClientAllowed(userTypes.ToHashSet(), client);

    [Fact]
    public void User_type_matching_the_client_id_is_allowed() =>
        Assert.True(Allowed(ClientNamed("B2C"), "B2C"));

    [Fact]
    public void Admin_is_allowed_on_any_client() =>
        Assert.True(Allowed(ClientNamed("mahzen-mobile-bff"), "Admin"));

    [Fact]
    public void Client_without_the_property_keeps_the_old_rule() =>
        Assert.False(Allowed(ClientNamed("mahzen-mobile-bff"), "B2C"));

    [Theory]
    [InlineData("B2C")]
    [InlineData("B2B, B2C")]
    [InlineData(" B2C ,")]
    public void Client_listing_the_user_type_is_allowed(string allowedUserTypes) =>
        Assert.True(Allowed(ClientNamed("mahzen-mobile-bff", allowedUserTypes), "B2C"));

    [Theory]
    [InlineData("B2B")]
    [InlineData("")]
    [InlineData("b2c")]
    public void Client_not_listing_the_user_type_is_rejected(string allowedUserTypes) =>
        Assert.False(Allowed(ClientNamed("mahzen-mobile-bff", allowedUserTypes), "B2C"));

    [Fact]
    public void User_without_a_type_is_rejected() =>
        Assert.False(Allowed(ClientNamed("mahzen-mobile-bff", "B2C")));
}

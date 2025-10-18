using IdentityServer4;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IdentityServer.SeedDatas
{
    public static class IdentityFrameworkSeedData
    {
        public static void Seed(ConfigurationDbContext context)
        {
            
            if (!context.Clients.Any())
            {
                var client1 = new Client
                {
                    ClientId = "client",

                    // no interactive user, use the clientid/secret for authentication
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPasswordAndClientCredentials,

                    // secret for authentication
                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                     },

                    // scopes that client has access to
                    AllowedScopes = { "api1.read", IdentityServerConstants.LocalApi.ScopeName },
                    AllowOfflineAccess=true
                    
                };

                var client2 = new Client
                {
                    //ClientId = "jobtr-react-client",
                    ClientId = "job-tracking-ui",
                    RequireClientSecret = false,
                    // no interactive user, use the clientid/secret for authentication
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                    ClientName = "Job Tracking Client",
                    // secret for authentication
                    

                    // scopes that client has access to
                    AllowedScopes = { "api1.read", IdentityServerConstants.LocalApi.ScopeName, "catalog_api.read", "catalog_api.update", "catalog_api.write", IdentityServerConstants.StandardScopes.Email, IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.OfflineAccess, "Roles", "gateway_api.fullpermission", "auth_api.fullpermission"},
                    AllowOfflineAccess = true,
                    //RedirectUris = { "http://localhost:3000/callback" },
                    AllowedCorsOrigins = { "http://localhost:3000" },
                    //PostLogoutRedirectUris = { "http://localhost:3000" }

                };
                context.Clients.Add(client1.ToEntity());
                context.Clients.Add(client2.ToEntity());
            }
            
            if (!context.IdentityResources.Any())
            {
                var list = new List<IdentityServer4.EntityFramework.Entities.IdentityResource>();
                list.Add(new IdentityResources.OpenId().ToEntity());
                list.Add(new IdentityResources.Profile().ToEntity());
                list.Add(new IdentityResource() { Name = "lastname", DisplayName = "Last Name", Description = "Kullanıcı soyadı.", UserClaims = new[] { "lastname" } }.ToEntity());
                list.Add(new IdentityResource() { Name = "firstname", DisplayName = "First Name", Description = "Kullanıcı adı.", UserClaims = new[] { "firstname" } }.ToEntity());
                list.Add(new IdentityResource() { Name = "email", DisplayName = "Email", Description = "Kullanıcı mail adresi.", UserClaims = new[] { "email" } }.ToEntity());
                list.Add(new IdentityResource() { Name = "username", DisplayName = "User Name", Description = "Kullanıcı özel adı.", UserClaims = new[] { "username" } }.ToEntity());
                list.Add(new IdentityResource() { Name = "tenantid", DisplayName = "Tenant Id", Description = "Kullanıcının ait olduğu şirket id numarası.", UserClaims = new[] { "tenantid" } }.ToEntity());
                ////ekelendi
                list.Add(new IdentityResource() { Name = "roles", DisplayName = "Roles", Description = "Kullanıcı rolleri.", UserClaims = new[] { "role" } }.ToEntity());

                context.IdentityResources.AddRange(list);
                
            }

            

            if (!context.ApiResources.Any())
            {
                var apiResources1 = new ApiResource("resource_catalog_api")
                {
                    Scopes = { "catalog_api.read", "catalog_api.write", "catalog_api.update" },
                    ApiSecrets = new[]{new  Secret("secret-catalog_api".Sha256())
                    }
                };

                var apiResources2 = new ApiResource("resource_api1")
                {
                    Scopes = { "api1.read", "api1.write", "api1.update" },
                    ApiSecrets = new[]{new  Secret("secretapi1".Sha256())
                    }
                };
                
                var apiResources3 = new ApiResource("resource_api_gateway")
                {
                    Scopes = { "gateway_api.fullpermission" },
                    ApiSecrets = new[]{new  Secret("secretapigateway".Sha256())
                    }
                };
                var apiResources5 = new ApiResource("resource_api_auth")
                {
                    UserClaims = { "tenantid" },
                    Scopes = { "auth_api.fullpermission" },
                    ApiSecrets = new[]{new  Secret("secretapiauth".Sha256())}
                };
                var apiResources4 = new ApiResource(IdentityServerConstants.LocalApi.ScopeName)
                {
                   
                };
                
                context.ApiResources.Add(apiResources1.ToEntity());
                context.ApiResources.Add(apiResources2.ToEntity());
                context.ApiResources.Add(apiResources3.ToEntity());
                context.ApiResources.Add(apiResources4.ToEntity());
                context.ApiResources.Add(apiResources5.ToEntity());
            }

            if (!context.ApiScopes.Any())
            {
                var apiScopes1 = new ApiScope("api1.read", "API 1 için okuma izni");
                var apiScopes2 = new ApiScope("api1.write", "API 1 için yazma izni");
                var apiScopes3 = new ApiScope("api1.update", "API 1 için güncelleme izni");
                var apiScopes4 = new ApiScope(IdentityServerConstants.LocalApi.ScopeName);

                var apiScopes5 = new ApiScope("catalog_api.read", "catalog_api için okuma izni");
                var apiScopes6 = new ApiScope("catalog_api.write", "catalog_api için yazma izni");
                var apiScopes7 = new ApiScope("catalog_api.update", "catalog_api için güncelleme izni");
                
                var apiScopes8 = new ApiScope("gateway_api.fullpermission", "gateway_api full yetki");
                var apiScopes9 = new ApiScope("auth_api.fullpermission", "auth_api full yetki");

                context.ApiScopes.Add(apiScopes1.ToEntity());
                context.ApiScopes.Add(apiScopes2.ToEntity());
                context.ApiScopes.Add(apiScopes3.ToEntity());
                context.ApiScopes.Add(apiScopes4.ToEntity());
                context.ApiScopes.Add(apiScopes5.ToEntity());
                context.ApiScopes.Add(apiScopes6.ToEntity());
                context.ApiScopes.Add(apiScopes7.ToEntity());
                context.ApiScopes.Add(apiScopes8.ToEntity());
                context.ApiScopes.Add(apiScopes9.ToEntity());
            }
            
            context.SaveChanges();

        }
       
    }
}

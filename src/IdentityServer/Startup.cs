// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4;
using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer.Services;
using IdentityServer.Services.Login;
using IdentityServer.Services.Role;
using AutoMapper;
using IdentityServer.Repositories;
using IdentityServer.Services.User;
using IdentityServer.Repositories.Identity;
using IdentityServer.Repositories.Hybrid;
using IdentityServer.Services.Profil;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityServer
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLocalApiAuthentication();
            services.AddControllersWithViews();
            services.AddCors();
            
            // Health checks - basit bir endpoint için
            services.AddHealthChecks();
            
            services.AddScoped<ILoginService, LoginService>();
            services.AddScoped<ICustomRepository, CustomRepository>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IIdentityRepository, IdentityRepository>();
            services.AddScoped<IHybridRepository, HybridRepository>();
            services.AddScoped<IUserChangeLogService, UserChangeLogService>();
            
            ///automapper

            services.AddAutoMapper(typeof(Startup));

            services.AddDbContext<CustomDbContext>(option =>
            {
                option.UseNpgsql(Configuration.GetConnectionString("DefaultConnection"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ids4"));
            });

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ids4")));

            var assemblyName = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
                {
                    // zorunlu karakter tipleri
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false; // istersen true yap
                    // minimum uzunluk
                    options.Password.RequiredLength = 8;
                    // benzersiz karakter sayısı + Tipik: 1
                    options.Password.RequiredUniqueChars = 1;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            var migrationsAssembly = typeof(Startup).Assembly.GetName().Name;
            
            var builder = services.AddIdentityServer()
                .AddConfigurationStore(options =>
                {
                    options.DefaultSchema = "ids4";
                    options.ConfigureDbContext = b =>
                        b.UseNpgsql(connectionString, sql =>
                        {
                            sql.MigrationsAssembly(migrationsAssembly);        
                            sql.MigrationsHistoryTable("__EFMigrationsHistory", "ids4");
                        });
                })
                .AddDeveloperSigningCredential()
                .AddAspNetIdentity<ApplicationUser>()
                .AddProfileService<CustomProfileService>();
                //.AddResourceOwnerValidator<ResourceOwnerPasswordValidator>();
                

            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                    
                    // register your IdentityServer with Google at https://console.developers.google.com
                    // enable the Google+ API
                    // set the redirect URI to https://localhost:5001/signin-google
                    options.ClientId = "copy client ID from Google here";
                    options.ClientSecret = "copy client secret from Google here";
                });
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }

            app.UseStaticFiles();
            app.UseCors(corsPolicyBuilder =>
                           corsPolicyBuilder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());

            app.UseRouting();

            app.UseIdentityServer();
            app.UseAuthentication();
            app.UseAuthorization();
            
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();

                // Health endpoint - returns a compact JSON with overall status and checks
                endpoints.MapHealthChecks("/health", new HealthCheckOptions
                {
                    ResponseWriter = async (context, report) =>
                    {

                        await context.Response.WriteAsync("health-test-1");
                    }
                });
            });
        }
    }
}
using System;
using System.IO;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IdentityServer.Data;

public class CustomDbContextFactory : IDesignTimeDbContextFactory<CustomDbContext>
{
    public CustomDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connStr = configuration.GetConnectionString("DefaultConnection");
        var migrationsAssembly = typeof(CustomDbContextFactory).Assembly.GetName().Name;

        var optionsBuilder = new DbContextOptionsBuilder<CustomDbContext>();

        optionsBuilder.UseNpgsql(connStr, npgsql =>
        {
            npgsql.MigrationsAssembly(migrationsAssembly);
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ids4");
        });

        return new CustomDbContext(optionsBuilder.Options); 
    }
}
using System;
using System.IO;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IdentityServer.Data;

public class PersistedGrantDbContextFactory 
    : IDesignTimeDbContextFactory<PersistedGrantDbContext>
{
    public PersistedGrantDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connStr = configuration.GetConnectionString("DefaultConnection");
        var migrationsAssembly = typeof(PersistedGrantDbContextFactory).Assembly.GetName().Name;

        var optionsBuilder = new DbContextOptionsBuilder<PersistedGrantDbContext>();

        optionsBuilder.UseNpgsql(connStr, npgsql =>
        {
            npgsql.MigrationsAssembly(migrationsAssembly);
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ids4");
        });

        var storeOptions = new OperationalStoreOptions
        {
            DefaultSchema = "ids4"
        };

        return new PersistedGrantDbContext(optionsBuilder.Options, storeOptions);
    }
}
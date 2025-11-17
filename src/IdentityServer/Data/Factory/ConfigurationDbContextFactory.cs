using System;
using System.IO;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IdentityServer.Data;

public class ConfigurationDbContextFactory 
    : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    public ConfigurationDbContext CreateDbContext(string[] args)
    {
        // 1) appsettings.json + environment dosyasını oku
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        // 2) Connection string’i appsettings’ten çek
        // appsettings.json içinde:
        // "ConnectionStrings": { "Ids4Db": "Host=...;Port=...;" }
        var connStr = config.GetConnectionString("DefaultConnection");
        var migrationsAssembly = typeof(ConfigurationDbContextFactory).Assembly.GetName().Name;
        var optionsBuilder = new DbContextOptionsBuilder<ConfigurationDbContext>();

        optionsBuilder.UseNpgsql(
            connStr,
            npgsql =>
            {
                npgsql.MigrationsAssembly(migrationsAssembly);         
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ids4");
            });

        var storeOptions = new ConfigurationStoreOptions
        {
            DefaultSchema = "ids4"
        };

        return new ConfigurationDbContext(optionsBuilder.Options, storeOptions);
    }
}
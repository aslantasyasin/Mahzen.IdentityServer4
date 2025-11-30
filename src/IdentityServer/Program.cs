// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer.SeedDatas;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Linq;

namespace IdentityServer
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
                .Enrich.FromLogContext()
                // uncomment to write to Azure diagnostics stream
                //.WriteTo.File(
                //    @"D:\home\LogFiles\Application\identityserver.txt",
                //    fileSizeLimitBytes: 1_000_000,
                //    rollOnFileSizeLimit: true,
                //    shared: true,
                //    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", theme: AnsiConsoleTheme.Code)
                .CreateLogger();

            try
            {
                var seed = args.Contains("/seed");
                if (seed)
                {
                    args = args.Except(new[] { "/seed" }).ToArray();
                }

                var host = CreateHostBuilder(args).Build();
                using(var serviceScope = host.Services.CreateScope())
                {
                    var services = serviceScope.ServiceProvider;

                    var context = services.GetRequiredService<ConfigurationDbContext>();
                    IdentityFrameworkSeedData.Seed(context);
                }
                
                if (seed)
                {
                    Log.Information("Seeding database...");
                    var config = host.Services.GetRequiredService<IConfiguration>();
                    var connectionString = config.GetConnectionString("DefaultConnection");
                    UserSeedData.EnsureSeedData(connectionString);
                    Log.Information("Done seeding database.");
                    return 0;
                }

                Log.Information("Starting host...");
                host.Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly.");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    // Kestrel ile HTTP (80) ve HTTPS (443) endpoint’lerini ayarla
                    webBuilder.ConfigureKestrel(options =>
                    {
                        // HTTP – istersen kapatabilirsin ama genelde iç trafiğe yarıyor
                        options.ListenAnyIP(80);

                        // HTTPS – PFX dosyasını /certs altından, şifreyi env’den al
                        options.ListenAnyIP(443, listenOptions =>
                        {
                            var certPath = "/certs/mahzen-ids4.pfx";
                            var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");

                            if (string.IsNullOrEmpty(certPassword))
                            {
                                // Muhtemelen local development ortamındasın:
                                // ASP.NET Core'un kendi dev sertifikasını kullan
                                // (önceden bir kere `dotnet dev-certs https --trust` çalıştırmış olman lazım)
                                listenOptions.UseHttps();
                            }
                            else
                            {
                                // Kubernetes / prod: PFX + şifre ile çalış
                                listenOptions.UseHttps(certPath, certPassword);
                            }
                        });
                    });

                    // Artık UseUrls'a gerek yok, endpoint’leri Kestrel üzerinden yönetiyoruz
                    webBuilder.UseStartup<Startup>();
                });
    }
}
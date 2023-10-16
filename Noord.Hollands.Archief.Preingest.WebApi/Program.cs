using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Noord.Hollands.Archief.Preingest.WebApi;

namespace Noord.Hollands.Archief.Preingest.WebApi
{
    /// <summary>
    /// Default entry point
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            Task.Run(() =>
            {
                CreateDbIfNotExists(host);
            });

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Host created.");
                
            host.Run();
        }

        /// <summary>
        /// Creates the host builder.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging=> 
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    // Probably unexpected: in PreIngestStatusContext the same code does not specify `optional: true,
                    // reloadOnChange: true`, and does not load the 2nd JSON file
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true); // optional extra provider

                    if (env.IsDevelopment()) // different providers in dev
                    {
                        /*
                        var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                        if (appAssembly != null)
                        {
                            config.AddUserSecrets(appAssembly, optional: true);
                        }
                        */
                    }

                    config.AddEnvironmentVariables(); // overwrites previous values

                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        /// <summary>
        /// Creates the database if not exists.
        /// </summary>
        /// <param name="host">The host.</param>
        private static void CreateDbIfNotExists(IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<Model.PreIngestStatusContext>();
                    context.Database.EnsureCreated();
                    // DbInitializer.Initialize(context);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred creating the DB.");
                }
            }
        }
    }
}

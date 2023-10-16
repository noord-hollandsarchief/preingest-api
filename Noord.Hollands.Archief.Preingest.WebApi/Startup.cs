using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;

using Noord.Hollands.Archief.Preingest.WebApi.Entities;
using Noord.Hollands.Archief.Preingest.WebApi.Handlers;
using Noord.Hollands.Archief.Preingest.WebApi.EventHub;

using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Noord.Hollands.Archief.Preingest.WebApi
{
    /// <summary>
    /// Default startup object
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public IConfiguration Configuration { get; }
       
        /// <summary>
        /// Configures the services. This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });
           
            services.AddSignalR();
            services.AddHealthChecks();            

            var appSettingsSection = Configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettingsSection);

            var settings = appSettingsSection.Get<AppSettings>();
            services.AddDbContext<Model.PreIngestStatusContext>(options => options.UseSqlite(Configuration.GetConnectionString("Sqlite")));
                        
            services.AddSingleton<PreingestEventHub>();
            services.AddSingleton<CollectionHandler>();

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen();
        }
         
        /// <summary>
        /// Configures the specified application. This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="env">The env.</param>
        /// <param name="logger">The logger.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {                
                app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                DefaultFileNames = new List<string> { "events.html", "collection.html", "collections.html" }
            });

            app.UseStaticFiles();
            app.UseRouting();

            var appSettingsSection = Configuration.GetSection("AppSettings");           
            var settings = appSettingsSection.Get<AppSettings>();
            var origins = settings.WithOrigins.Split(";");

            app.UseCors(x => x.AllowAnyOrigin().AllowCredentials().AllowAnyMethod().AllowAnyHeader().WithOrigins(origins));

            app.UseStatusCodePages();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
                endpoints.MapHub<PreingestEventHub>("/preingestEventHub");
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();
            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Preingest API");
            });
        }
    }
}

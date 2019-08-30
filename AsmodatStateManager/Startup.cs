using AsmodatStateManager.Processing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using AsmodatStandard.Extensions;
using AsmodatStateManager.Services;
using Microsoft.AspNetCore.Authentication;
using AsmodatStateManager.Handlers;
using System;
using AsmodatStateManager.Model;
using Amazon.Runtime;

namespace AsmodatStateManager
{
    public class Startup
    {
        public IConfiguration Configuration { get; set; }

        public Startup(IConfiguration configuration)
        {
            var cmd = Environment.GetCommandLineArgs();

            if(cmd.Length < 3)
                throw new Exception("Missing Port or Configuartaion File Name!");

            var configJson = cmd[2];

            if (configJson.IsNullOrEmpty())
            {
                Configuration = configuration;
                return;
            } else if (!File.Exists(configJson))
                throw new Exception($"Configuration file does NOT exist: '{configJson}'");

            var builder = new ConfigurationBuilder()
                .AddJsonFile(configJson, optional: true, reloadOnChange: true);

            Configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                    });
            });
  
            services.AddMvc();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>(); //required for client IP discovery

            services.AddOptions();

            services.Configure<ManagerConfig>(Configuration.GetSection("ManagerConfig"));

            services.AddSingleton<PerformanceManager>();
            services.AddSingleton<SyncManager>();

            services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, DiskPerformanceService>();
            services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, SyncService>();

            // configure basic authentication 
            services.AddAuthentication("BasicAuthentication")
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseCors("AllowAll");

            app.UseAuthentication();

            app.UseMvc(routes => {
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}

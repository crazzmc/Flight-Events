using FlightEvents.Data;
using FlightEvents.Web.GraphQL;
using FlightEvents.Web.Hubs;
using FlightEvents.Web.Logics;
using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Voyager;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlightEvents.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions<DiscordOptions>().Bind(Configuration.GetSection("Discord")).ValidateDataAnnotations();
            services.AddOptions<AzureBlobOptions>().Bind(Configuration.GetSection("FlightPlan:AzureStorage")).ValidateDataAnnotations();
            services.AddOptions<AzureTableOptions>().Bind(Configuration.GetSection("FlightPlan:AzureStorage")).ValidateDataAnnotations();
            services.AddOptions<BroadcastOptions>().Bind(Configuration.GetSection("Broadcast")).ValidateDataAnnotations();

            services.AddSingleton<RandomStringGenerator>();
            services.AddSingleton<IFlightEventStorage>(sp => new JsonFileFlightEventStorage("events.json", sp.GetService<RandomStringGenerator>()));
            services.AddSingleton<IFlightPlanStorage, AzureBlobFlightPlanStorage>();
            services.AddSingleton<IAirportStorage, XmlFileAirportStorage>();
            services.AddSingleton<IDiscordConnectionStorage, AzureTableDiscordConnectionStorage>();

            services.AddGraphQL(
                SchemaBuilder.New()
                    .AddQueryType<QueryType>()
                    .AddMutationType<MutationType>()
                    .AddType<FlightEventType>()
                    );

            services.AddControllersWithViews();
            services.AddRazorPages();

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });

            var builder = services.AddSignalR();
            if (!string.IsNullOrWhiteSpace(Configuration["Azure:SignalR:ConnectionString"]))
            {
                builder.AddAzureSignalR();
            }
            builder.AddMessagePackProtocol();

            services.AddHttpClient<DiscordLogic>();

            services.AddHostedService<StatusBroadcastService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app
                .UseGraphQL("/graphql")
                .UsePlayground("/graphql")
                .UseVoyager("/graphql");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
                endpoints.MapRazorPages();

                endpoints.MapHub<FlightEventHub>("/FlightEventHub");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }
    }
}

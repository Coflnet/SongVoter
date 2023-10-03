/*
 * Songvoter
 *
 * Definition for songvoter API
 *
 * The version of the OpenAPI document: 0.0.1
 * Contact: support@coflnet.com
 * Generated by: https://openapi-generator.tech
 */

using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Coflnet.SongVoter.Authentication;
using Coflnet.SongVoter.Filters;
using Coflnet.SongVoter.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Coflnet.SongVoter.Transformers;
using Coflnet.SongVoter.Service;
using Coflnet.Core;

namespace Coflnet.SongVoter
{
    /// <summary>
    /// Startup
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration"></param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// The application configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        private static string CORS_PLICY_NAME = "defaultCorsPolicy";

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IAuthorizationHandler, ApiKeyRequirementHandler>();


            // Add framework services.
            services
                // Don't need the full MVC stack for an API, see https://andrewlock.net/comparing-startup-between-the-asp-net-core-3-templates/
                .AddControllers()
                .AddNewtonsoftJson(opts =>
                {
                    opts.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    opts.SerializerSettings.Converters.Add(new StringEnumConverter
                    {
                        NamingStrategy = new CamelCaseNamingStrategy()
                    });
                });

            services
                .AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("0.0.1", new OpenApiInfo
                    {
                        Title = "Songvoter",
                        Description = "Songvoter",
                        TermsOfService = new Uri("https://coflnet.com/terms/"),
                        Contact = new OpenApiContact
                        {
                            Name = "Coflnet",
                            Email = "support@coflnet.com"
                        },
                        License = new OpenApiLicense
                        {
                            Name = "AGPL",
                            Url = new Uri("https://github.com/Coflnet/songvoter/blob/main/LICENSE")
                        },
                        Version = "0.0.1",
                    });
                    c.CustomSchemaIds(type => type.FriendlyId(true));
                    c.IncludeXmlComments($"{AppContext.BaseDirectory}{Path.DirectorySeparatorChar}{Assembly.GetEntryAssembly().GetName().Name}.xml");
                    // Sets the basePath property in the OpenAPI document generated
                    //c.DocumentFilter<BasePathFilter>("");

                    // Include DataAnnotation attributes on Controller Action parameters as OpenAPI validation rules (e.g required, pattern, ..)
                    // Use [ValidateModelState] on Actions to actually validate it in C# as well!
                    c.OperationFilter<GeneratePathParamsValidationFilter>();
                });
            services
                .AddSwaggerGenNewtonsoftSupport();

            services.AddCors(o =>
            {
                o.AddPolicy(CORS_PLICY_NAME, p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
            });


            string key = Configuration["jwt:secret"]; //this should be same which is used while creating token      
            var issuer = "http://mysite.com"; //this should be same which is used while creating token  

            // allow anyone without a token for testing
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = issuer,
                        ValidAudience = issuer,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                            {
                                context.Response.Headers.Add("Token-Expired", "true");
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddDbContextPool<DBModels.SVContext>(options =>
            {
                options.UseNpgsql(Configuration["DB_CONNECTION"]);
            });
            services.AddSingleton<SongTransformer>();
            services.AddSingleton<IDService>();
            services.AddTransient<SpotifyService>();
            services.AddHealthChecks();
            services.AddCoflnetCore();
            services.AddHealthChecks().AddCheck<DbHealthCheck>("db_health_check");

            Console.WriteLine("registered all");
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }
            app.UseCoflnetCore();
            app.UseMiddleware<ErrorMiddleware>();

            app.UseSwagger(c =>
                {
                    c.RouteTemplate = "api/openapi/{documentName}/openapi.json";
                })
                .UseSwaggerUI(c =>
                {
                    // set route prefix to api, e.g. http://localhost:8080/api/index.html
                    c.RoutePrefix = "api";
                    c.SwaggerEndpoint("/api/openapi/0.0.1/openapi.json", "Songvoter");
                });
            app.UseRouting();
            app.UseCors(CORS_PLICY_NAME);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapHealthChecks("/status");
                });
        }
    }
}

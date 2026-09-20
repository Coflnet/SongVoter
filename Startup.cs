using System;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Transformers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Coflnet.SongVoter;

public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers().AddNewtonsoftJson(options => {
            options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            options.SerializerSettings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy()));
        });
        services.AddSwaggerGen(options => {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "SongVoter", Version = "v1" });
            options.CustomSchemaIds(type => type.FullName);
        }).AddSwaggerGenNewtonsoftSupport();
        var secret = configuration["jwt:secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32 || secret == "changethistosomethingrandomplease")
            throw new InvalidOperationException("Set jwt__secret to a random secret of at least 32 bytes.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
            options.Events = new JwtBearerEvents {
                OnTokenValidated = async context => {
                    var ids = context.HttpContext.RequestServices.GetRequiredService<IDService>();
                    var db = context.HttpContext.RequestServices.GetRequiredService<SVContext>();
                    var id = ids.FromHash(context.Principal.FindFirst("uid")?.Value);
                    if (!await db.Users.AnyAsync(u => u.Id == id)) context.Fail("Profile no longer exists.");
                }
            };
            options.TokenValidationParameters = new TokenValidationParameters {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = GuestAuthentication.Issuer, ValidAudience = GuestAuthentication.Issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddRateLimiter(options => {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("imports", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("uid")?.Value ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(10) }));
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                // A venue's guests can share one public IP; joining takes two requests.
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(10) }));
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst("uid")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions {
                        PermitLimit = context.User.Identity?.IsAuthenticated == true ? 240 : 600,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });
        services.Configure<ForwardedHeadersOptions>(options => {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2; // Ingress, then the website's same-origin proxy.
            foreach (var network in configuration.GetSection("TrustedProxyNetworks").Get<string[]>() ?? [])
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(configuration.GetSection("CorsOrigins").Get<string[]>() ?? ["https://songvoter.party"])
            .AllowAnyHeader().AllowAnyMethod()));
        services.AddDbContext<SVContext>(options => options.UseNpgsql(configuration["DB_CONNECTION"]));
        services.AddSingleton<IDService>();
        services.AddSingleton<SongTransformer>();
        services.AddSingleton<GuestAuthentication>();
        services.AddScoped<IMusicCatalog, YoutubeCatalog>();
        services.AddScoped<IMusicCatalog, SpotifyCatalog>();
        services.AddScoped<SongCatalog>();
        services.AddScoped<MusicImport>();
        services.AddHttpClient("music-import", client => client.Timeout = TimeSpan.FromSeconds(15))
            .RemoveAllLoggers() // Provider request URLs may contain the YouTube API key.
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<PartyService>();
        services.AddHealthChecks().AddCheck<DbHealthCheck>("database");
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.UseForwardedHeaders();
        if (!environment.IsDevelopment()) app.UseHsts();
        app.UseMiddleware<ErrorMiddleware>();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseSwagger(options => options.RouteTemplate = "api/openapi/{documentName}.json");
        app.UseSwaggerUI(options => {
            options.RoutePrefix = "api/docs";
            options.SwaggerEndpoint("/api/openapi/v1.json", "SongVoter");
        });
        app.UseEndpoints(endpoints => {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/status").AllowAnonymous();
            endpoints.MapGet("/health/live", () => Results.Ok()).AllowAnonymous();
        });
    }
}

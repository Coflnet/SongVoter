using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Coflnet.Security.OpenBao;
using Coflnet.SongVoter.DBModels;

namespace Coflnet.SongVoter;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var host = CreateHostBuilder(args.Where(a => a != "--migrate-only").ToArray()).Build();
        if (args.Contains("--migrate-only")) {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SVContext>().Database.MigrateAsync();
            return;
        }
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((_, config) => config.AddOpenBaoFromEnvironment())
        .ConfigureWebHostDefaults(builder => builder.UseStartup<Startup>()
            .UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:4200"));
}

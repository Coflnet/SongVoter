using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Coflnet.Security.OpenBao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SongVoter.Tests;

public class OpenBaoTests
{
    [Theory, Trait("Category", "Unit")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicCaLoadsWithoutPrivateKeyAndRejectsUntrustedServer(bool trusted)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var serverCert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var otherCert = new CertificateRequest("CN=other", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var directory = Directory.CreateTempSubdirectory("songvoter-bao-test-");
        try {
            var ca = Path.Combine(directory.FullName, "ca.crt");
            var jwt = Path.Combine(directory.FullName, "token");
            await File.WriteAllTextAsync(ca, (trusted ? serverCert : otherCert).ExportCertificatePem());
            await File.WriteAllTextAsync(jwt, "test-jwt");
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(serverCert)));
            await using var app = builder.Build();
            app.MapPost("/v1/auth/kubernetes/login", () => new { auth = new { client_token = "test-token" } });
            app.MapGet("/v1/kv/data/services/songvoter", () => new { data = new { data = new { jwt__secret = "test-value" } } });
            await app.StartAsync();
            using var provider = new OpenBaoConfigurationProvider(new OpenBaoOptions {
                Address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single(),
                Role = "songvoter", Path = "services/songvoter", CaCert = ca, TokenPath = jwt, ReloadInterval = TimeSpan.Zero
            });
            if (trusted) {
                provider.Load();
                Assert.True(provider.TryGet("jwt:secret", out var value));
                Assert.Equal("test-value", value);
            } else {
                Assert.Throws<HttpRequestException>(provider.Load);
            }
        } finally {
            directory.Delete(true);
        }
    }
}

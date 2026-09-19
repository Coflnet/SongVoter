using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Xunit;

namespace SongVoter.Tests;

public class AuthenticationTests
{
    [Fact, Trait("Category", "Unit")]
    public void ProofIsBoundToChallengeIdentityAndExpiry()
    {
        var secret = new string('a', 64);
        var challenge = new AuthChallenge { Id = "challenge", IdentityHash = GuestAuthentication.IdentityHash(secret),
            Difficulty = 16, ExpiresAt = DateTime.UtcNow.AddMinutes(1) };
        long counter = 0;
        while (!GuestAuthentication.Verify(challenge, secret, counter, DateTime.UtcNow)) counter++;
        Assert.True(GuestAuthentication.Verify(challenge, secret, counter, DateTime.UtcNow));
        Assert.False(GuestAuthentication.Verify(challenge, new string('b', 64), counter, DateTime.UtcNow));
        Assert.False(GuestAuthentication.Verify(challenge, secret, counter, challenge.ExpiresAt));
        Assert.False(GuestAuthentication.Verify(challenge, secret, -1, DateTime.UtcNow));
        challenge.Id = "another-challenge";
        Assert.False(GuestAuthentication.Verify(challenge, secret, counter, DateTime.UtcNow));
    }
}

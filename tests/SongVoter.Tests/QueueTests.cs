using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Xunit;

namespace SongVoter.Tests;

public class QueueTests
{
    [Fact, Trait("Category", "Unit")]
    public void GuestsHaveLowerWeightAndCannotMultiplyInfluenceByAddingSongs()
    {
        var host = new User { Id = 1 };
        var guest = new User { Id = 2 };
        var party = new Party { Creator = host, Members = [guest] };
        var hostSong = new PartySong { Id = 1, UpVoters = [host] };
        var guestSongs = Enumerable.Range(2, 30).Select(i => new PartySong { Id = i, UpVoters = [guest] }).ToArray();
        var queue = guestSongs.Append(hostSong).ToArray();
        Assert.Equal(3, PartyService.Score(hostSong, party, queue));
        Assert.Equal(1, guestSongs.Sum(s => PartyService.Score(s, party, queue)), 6);
        Assert.Same(hostSong, PartyService.Ranked(party, queue).First());
        hostSong.PlayedTimes = 1;
        Assert.NotSame(hostSong, PartyService.Ranked(party, queue).First());
        party.Members.Clear();
        Assert.Equal(0, PartyService.Score(guestSongs[0], party, queue));
    }
}

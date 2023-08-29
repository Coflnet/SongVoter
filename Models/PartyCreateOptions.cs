
using Coflnet.SongVoter.DBModels;

namespace Coflnet.SongVoter.Models;
public class PartyCreateOptions
{
    /// <summary>
    /// Name that the party should have 
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// What platforms are supported by this party
    /// </summary>
    public Platforms SupportedPlatforms { get; set; }
}
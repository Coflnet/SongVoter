using System;

namespace Coflnet.SongVoter.DBModels;
/// <summary>
/// Invatations to parties
/// </summary>
public class Invite
{
    /// <summary>
    /// The id of this invite
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The party this invite is for
    /// </summary>
    public Party Party { get; set; }
    public User User { get; set; }
    /// <summary>
    /// The user that created this invite
    /// </summary>
    public int CreatorId { get; set; }
    /// <summary>
    /// The time this invite is valid until
    /// </summary>
    public DateTime ValidUntil { get; set; }
    /// <summary>
    /// How often this invite has been used
    /// </summary>
    public int UsageCount { get; set; }
    /// <summary>
    /// How often this invite can be used
    /// </summary>
    public int UsageLimit { get; set; }
}
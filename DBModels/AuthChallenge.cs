using System;
using System.ComponentModel.DataAnnotations;

namespace Coflnet.SongVoter.DBModels;

public class AuthChallenge
{
    [MaxLength(64)] public string Id { get; set; }
    [MaxLength(64)] public string IdentityHash { get; set; }
    public int Difficulty { get; set; }
    public DateTime ExpiresAt { get; set; }
}

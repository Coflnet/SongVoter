using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Coflnet.SongVoter.DBModels;
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string GoogleId { get; set; }

    /// <summary>
    /// The songs this user upvoted
    /// </summary>
    public ICollection<PartySong> Upvotes { get; set; }
    /// <summary>
    /// The songs this user downvoted
    /// </summary>
    public ICollection<PartySong> Downvotes { get; set; }
    /// <summary>
    /// The parties this user is a member of
    /// </summary>
    public ICollection<Party> Parties { get; set; }
    /// <summary>
    /// The tokens to platforms a user can login with
    /// </summary>
    public ICollection<Oauth2Token> Tokens { get; set; }

    public override bool Equals(object obj)
    {
        return obj is User user &&
               Id == user.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, GoogleId);
    }
}

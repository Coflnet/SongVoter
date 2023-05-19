using System;

namespace Coflnet.SongVoter.DBModels;

/// <summary>
/// The tokens to platforms a user can login with
/// </summary>
public class Oauth2Token
{
    /// <summary>
    /// The id of this token
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The user this token is for
    /// </summary>
    public User User { get; set; }
    /// <summary>
    /// The platform this token is for
    /// </summary>
    public Platforms Platform { get; set; }
    /// <summary>
    /// The auth code to get the access token
    /// </summary>
    public string AuthCode { get; set; }
    /// <summary>
    /// The access token to access the api
    /// </summary>
    public string AccessToken { get; set; }
    /// <summary>
    /// The refresh token to get a new access token
    /// </summary>
    public string RefreshToken { get; set; }
    /// <summary>
    /// External id of the user
    /// </summary>
    public string ExternalId { get; set; }
    /// <summary>
    /// The scopes this token has access to, seperated by comma
    /// </summary>
    public string Scropes { get; set; }
    /// <summary>
    /// The time this token expires
    /// </summary>
    public DateTime Expiration { get; set; }
}
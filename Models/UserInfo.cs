/*
 * Songvoter
 *
 * Definition for songvoter API
 *
 * The version of the OpenAPI document: 0.0.1
 * Contact: support@coflnet.com
 * Generated by: https://openapi-generator.tech
 */
using System;

namespace Coflnet.SongVoter.Models;

public class UserInfo
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string SpotifyToken { get; set; }
    public DateTime? SpotifyTokenExpiration { get; set; }
}
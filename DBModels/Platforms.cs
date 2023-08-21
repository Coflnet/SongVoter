using System;

namespace Coflnet.SongVoter.DBModels;
/// <summary>
/// 
/// </summary>
[Flags]
public enum Platforms
{
    /// <summary>
    /// Probably an error
    /// </summary>
    Unkown,
    /// <summary>
    /// Videos from youtube
    /// </summary>
    Youtube,
    /// <summary>
    /// Songs from spotify
    /// </summary>
    Spotify,
    /// <summary>
    /// Placeholder for the next platform, marking that this is a flag enum
    /// </summary>
    Next = 4
}
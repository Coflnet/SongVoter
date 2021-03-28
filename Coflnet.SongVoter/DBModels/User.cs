using Microsoft.AspNetCore.Identity;

namespace Coflnet.SongVoter.DBModels
{
    public class User : IdentityUser<int>
    {
        public string GoogleId {get;set;}
    }
}

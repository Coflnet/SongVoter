using Microsoft.AspNetCore.Identity;

namespace Coflnet.SongVoter.DBModels
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string GoogleId { get; set; }
    }
}

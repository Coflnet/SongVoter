using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Coflnet.SongVoter.DBModels
{
    public class Party
    {
        public int Id { get; set; }
        public User Creator { get; set; }
        [MaxLength(30)]
        public string Name { get; set; }
        public List<User> Members { get; set; }
        public List<PartySong> Songs { get; set; }
    }
}

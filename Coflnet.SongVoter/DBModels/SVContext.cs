using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.DBModels
{
    public class SVContext : DbContext
    {
        public SVContext(DbContextOptions<SVContext> options) : base(options)
        {
        }

        public DbSet<Song> Songs { get; set; }
        public DbSet<User> Users { get; set; }
    }
}

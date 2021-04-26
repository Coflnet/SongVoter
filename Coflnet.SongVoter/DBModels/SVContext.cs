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
        public DbSet<Playlist> PlayLists { get; set; }
        public DbSet<ExternalSong> ExternalSongs { get; set; }
        public DbSet<Party> Parties { get; set; }
        public DbSet<PartySong> PartySongs { get; set; }
        public DbSet<Invite> Invites { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PartySong>()
                .HasOne<Party>("partyId");
            modelBuilder.Entity<PartySong>()
                .HasOne<Song>("songId");
        }
    }
}

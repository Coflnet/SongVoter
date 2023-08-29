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
                .HasMany(s => s.DownVoters)
                .WithMany(p => p.Downvotes);
            modelBuilder.Entity<PartySong>()
                .HasMany(s => s.UpVoters)
                .WithMany(p => p.Upvotes);

            modelBuilder.Entity<Party>()
                .HasOne(u => u.Creator);

            modelBuilder.Entity<Party>()
                .HasMany(p => p.Members)
                .WithMany(u => u.Parties);

            modelBuilder.Entity<Song>(c => 
                c.HasIndex(c => c.Lookup));
        }
    }
}

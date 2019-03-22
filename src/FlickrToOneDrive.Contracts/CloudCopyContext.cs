using Microsoft.EntityFrameworkCore;

namespace FlickrToOneDrive.Contracts
{
    public class CloudCopyContext : DbContext
    {
        public DbSet<File> Files { get; set; }
        public DbSet<Session> Sessions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename = cloudcopy.db");
        }
    }
}

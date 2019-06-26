using FlickrToCloud.Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace FlickrToCloud.Contracts
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

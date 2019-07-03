using System;
using FlickrToCloud.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlickrToCloud.Tests
{
    public class DatabaseFixture : IDisposable
    {
        public DatabaseFixture()
        {
            using (var db = new CloudCopyContext())
            {
                db.Database.Migrate();
            }
        }

        public void Dispose()
        {
            using (var db = new CloudCopyContext())
            {
                db.Database.EnsureDeleted();
            }
        }
    }
}


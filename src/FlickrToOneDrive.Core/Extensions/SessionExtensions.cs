using System.Collections.Generic;
using System.Linq;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Exceptions;
using FlickrToOneDrive.Contracts.Models;

namespace FlickrToOneDrive.Core.Extensions
{
    public static class SessionExtensions
    {
        public static void UpdateState(this Session session, SessionState state)
        {
            using (var db = new CloudCopyContext())
            {
                var dbSession = db.Sessions.FirstOrDefault(s => s.Id == session.Id);
                if (dbSession == null)
                    throw new CloudCopyException("Session does not exist");

                dbSession.State = state;
                session.State = state;
                db.SaveChanges();
            }
        }

        public static IList<File> GetFiles(this Session session, FileState state)
        {
            using (var db = new CloudCopyContext())
            {
                return db.Files.Where(f => f.SessionId == session.Id && (f.State == state)).ToList();
            }
        }

        public static void Delete(this Session session)
        {
            using (var db = new CloudCopyContext())
            {
                db.Sessions.Remove(session);
                db.SaveChanges();
            }
        }

    }
}

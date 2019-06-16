using System.Collections.Generic;
using System.Linq;
using FlickrToOneDrive.Contracts;
using FlickrToOneDrive.Contracts.Models;
using MvvmCross.ViewModels;

namespace FlickrToOneDrive.Core.ViewModels
{
    public class FilesViewModel : MvxViewModel<Setup>
    {
        private List<File> _files;
        private Setup _setup;

        public override void Prepare(Setup setup)
        {
            base.Prepare();
            _setup = setup;
            
            using (var db = new CloudCopyContext())
            {
                _files = db.Files.Where(f => f.SessionId == _setup.Session.Id).ToList();
            }
        }

        public Session Session
        {
            get { return _setup.Session; }
        }

        public ICollection<File> Files => _files;
    }
}

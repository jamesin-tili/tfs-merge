using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.ConnectionSetup
{
    class BranchViewModel : PropertyChangedBase
    {
        public BranchViewModel(string path, bool isEnabled)
        {
            Path = path;
            IsEnabled = isEnabled;
        }

        private string _path;
        public string Path
        {
            get { return _path; }
            set
            {
                if (value != _path)
                {
                    _path = value;
                    NotifyOfPropertyChange(() => Path);
                }
            }
        }

        private bool isEnabled;
        public bool IsEnabled
        {
            get { return isEnabled; }
            set
            {
                if (value != isEnabled)
                {
                    isEnabled = value;
                    NotifyOfPropertyChange(() => IsEnabled);
                }
            }
        }
    }
}

using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TFSMergingTool.Resources.FolderTree
{
    public partial class FolderTreeViewModel
    {
        public class FolderViewModel : PropertyChangedBase
        {
            public FolderViewModel(string path)
                : this(path, null)
            {
            }

            private FolderViewModel(string path, FolderViewModel parent)
            {
                if (path == _placeholderName)
                {
                    Name = path;
                    Path = path;
                    SubFolders = new BindableCollection<FolderViewModel>();
                }
                else
                {
                    var dInfo = new DirectoryInfo(path);
                    Name = dInfo.Name;
                    Path = path;
                    SubFolders = new BindableCollection<FolderViewModel>();
                    SubFolders.Add(new FolderViewModel(_placeholderName, this));
                }
                _parent = parent;
                IsSelected = false;
                IsExpanded = false;
            }

            private const string _placeholderName = "Loading...";
            private FolderViewModel _parent;

            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    NotifyOfPropertyChange(() => Name);
                }
            }

            public string Path { get; set; }

            public IObservableCollection<FolderViewModel> SubFolders { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    NotifyOfPropertyChange(() => IsSelected);
                }
            }

            private bool _isExpanded;
            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    if (value != _isExpanded)
                    {
                        if (value == true && SubFolders.First().Name == _placeholderName)
                        {
                            ExpandOneLevelOfSubfolders();
                        }
                        _isExpanded = value;
                        NotifyOfPropertyChange(() => IsExpanded);
                    }

                    // Expand all the way up to the root.
                    if (_isExpanded && _parent != null)
                        _parent.IsExpanded = true;
                }
            }

            public void ExpandParents()
            {
                if (_parent != null && !_parent.IsExpanded)
                    _parent.IsExpanded = true;
            }

            public void CloseAll()
            {
                if (_isExpanded)
                    IsExpanded = false;
                foreach (var subfolder in SubFolders)
                {
                    subfolder.CloseAll();
                }
            }

            public void DeselectAll()
            {
                if (_isSelected)
                    IsSelected = false;
                foreach (var subfolder in SubFolders)
                {
                    subfolder.DeselectAll();
                }
            }

            private void ExpandOneLevelOfSubfolders()
            {
                if (SubFolders.Any())
                    SubFolders.Clear();
                var dInfo = new DirectoryInfo(Path);
                var subfolders = dInfo.GetDirectories();
                foreach (var subfolder in subfolders)
                {
                    // Ignore TFS folders
                    if (subfolder.Name != "$tf")
                    {
                        var folderPath = subfolder.FullName;
                        SubFolders.Add(new FolderViewModel(folderPath, this));
                    }
                }
            }

            public FolderViewModel FindSelectedFolder()
            {
                if (this.IsSelected)
                    return this;
                foreach (var subfolder in SubFolders)
                {
                    var foundFolder = subfolder.FindSelectedFolder();
                    if (foundFolder != null)
                        return foundFolder;
                }
                return null;
            }

            public FolderViewModel FindFolderWithPath(string path)
            {
                if (this.Path == path)
                    return this;
                if (path.Contains(this.Path))
                {
                    if (SubFolders.First().Name == _placeholderName)
                        ExpandOneLevelOfSubfolders();
                    foreach (var subfolder in SubFolders)
                    {
                        var foundFolder = subfolder.FindFolderWithPath(path);
                        if (foundFolder != null)
                            return foundFolder;
                    }
                }
                return null;
            }

        }
    }
}

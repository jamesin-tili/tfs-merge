using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TFSMergingTool.Resources.FolderTree
{
    public partial class FolderTreeViewModel : PropertyChangedBase, IFolderTreeViewModel
    {
        public IObservableCollection<FolderViewModel> Folders { get; set; }


        /// <summary>
        /// Get or set the currently selected folder. Only the FullPath is considered when setting.
        /// </summary>
        public DirectoryInfo SelectedFolder
        {
            get
            {
                foreach (var folder in Folders)
                {
                    FolderViewModel foundFolder = folder.FindSelectedFolder();
                    if (foundFolder != null)
                        return new DirectoryInfo(foundFolder.Path);
                }
                return null;
            }
            set
            {
                if (!Directory.Exists(value.FullName))
                    throw new ArgumentException("Directory " + value.FullName + " does not exist.");
                FolderViewModel desiredFolder = null;
                foreach (var folder in Folders)
                {
                    var foundFolder = folder.FindFolderWithPath(value.FullName);
                    if (foundFolder != null)
                        desiredFolder = foundFolder;
                }
                if (desiredFolder != null)
                {
                    foreach (var folder in Folders)
                    {
                        folder.CloseAll();
                        folder.DeselectAll();
                    }
                    desiredFolder.ExpandParents();
                    desiredFolder.IsSelected = true;
                }
            }
        }


        public FolderTreeViewModel(string basePath)
        {
            if (!Directory.Exists(basePath))
                throw new ArgumentException("Directory " + basePath + " does not exist.");
            var basefolder = new FolderViewModel(basePath);
            Folders = new BindableCollection<FolderViewModel>();
            Folders.Add(basefolder);
        }

        public FolderTreeViewModel(string[] basePaths)
        {
            Folders = new BindableCollection<FolderViewModel>();
            foreach (var basePath in basePaths)
            {
                if (!Directory.Exists(basePath))
                    throw new ArgumentException("Directory " + basePath + " does not exist.");
                var basefolder = new FolderViewModel(basePath);
                Folders.Add(basefolder);
            }
        }

        public void SelectedChanged(object selectedItem)
        {
            string name;
            string path;
            if (selectedItem != null)
            {
                var selected = selectedItem as FolderViewModel;
                name = selected.Name;
                path = selected.Path;
            }
            else
            {
                name = string.Empty;
                path = string.Empty;
            }
            var args = new TreeViewSelectionChangedEventArgs()
            {
                Name = name,
                FullPath = path
            };
            OnSelectionChanged(args);
        }

        public event EventHandler<TreeViewSelectionChangedEventArgs> SelectionChanged;

        protected virtual void OnSelectionChanged(TreeViewSelectionChangedEventArgs e)
        {
            var handler = SelectionChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.Resources.FolderTree
{
    public interface IFolderTreeViewModel
    {
        DirectoryInfo SelectedFolder { get; set; }
        event EventHandler<TreeViewSelectionChangedEventArgs> SelectionChanged;
    }

    public class TreeViewSelectionChangedEventArgs : EventArgs
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }
}

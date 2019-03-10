using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.Merging
{
    class EditBranchOrderViewModel : Screen
    {

        public class BranchlistItem
        {
            public string Path { get; set; }
        }

        public List<string> Result { get; private set; }

        public EditBranchOrderViewModel(List<string> initialItems)
        {
            Branches = new BindableCollection<BranchlistItem>();
            foreach (var item in initialItems)
            {
                Branches.Add(new BranchlistItem() { Path = item });
            }
        }

        private IObservableCollection<BranchlistItem> _branches;
        public IObservableCollection<BranchlistItem> Branches
        {
            get { return _branches; }
            set
            {
                if (value != _branches)
                {
                    _branches = value;
                    NotifyOfPropertyChange(() => Branches);
                }
            }
        }

        private BranchlistItem _selectedBranches;
        public BranchlistItem SelectedBranches
        {
            get { return _selectedBranches; }
            set
            {
                if (value != _selectedBranches)
                {
                    _selectedBranches = value;
                    NotifyOfPropertyChange(() => SelectedBranches);
                }
            }
        }

        public void AddBranch()
        {
            var itemId = Branches.Count + 1;
            var item = new BranchlistItem() { Path = "Test Item #" + itemId };
            Branches.Add(item);
            SelectedBranches = item;
        }

        public void RemoveBranch()
        {
            var index = Branches.IndexOf(SelectedBranches);
            if (index >= 0)
            {
                Branches.RemoveAt(index);
            }
        }

        public void Close()
        {
            Result = Branches.Select(item => item.Path).ToList();
            TryClose(true);
        }

        public void MoveBranchUp()
        {
            var item = SelectedBranches;
            var index = Branches.IndexOf(item);
            if (index > 0)
            {
                Branches.RemoveAt(index);
                Branches.Insert(index - 1, item);
                SelectedBranches = item;
            }
        }

        public void MoveBranchDown()
        {
            var item = SelectedBranches;
            var index = Branches.IndexOf(item);
            if (index < Branches.Count - 1)
            {
                Branches.RemoveAt(index);
                Branches.Insert(index + 1, item);
                SelectedBranches = item;
            }
        }


    }
}

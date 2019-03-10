using Caliburn.Micro;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TFSMergingTool.OutputWindow;
using TFSMergingTool.Resources;
using TFSMergingTool.Settings;
using TFSMergingTool.Shell;

namespace TFSMergingTool.Merging
{
    public interface IMergeSpecificIdViewModel : IScreen
    {
        List<DirectoryInfo> BranchList { get; set; }
    }

    [Export(typeof(IMergeSpecificIdViewModel))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class MergeSpecificIdViewModel : Screen, IMergeSpecificIdViewModel
    {
        public List<DirectoryInfo> BranchList { get; set; }
        IEventAggregator EventAggregator { get; set; }
        IOutputWindow Output { get; set; }
        UserSettings Settings { get; set; }
        MyTfsConnection TfsConnection { get; set; }
        IPopupService Popups { get; set; }
        IShell Shell { get; set; }


        [ImportingConstructor]
        public MergeSpecificIdViewModel(IEventAggregator eventAggregator, IOutputWindow output, UserSettings settings, MyTfsConnection tfsConnection,
            IPopupService popups)
        {
            BranchList = new List<DirectoryInfo>();
            EventAggregator = eventAggregator;
            Output = output;
            Settings = settings;
            TfsConnection = tfsConnection;
            Popups = popups;
            Activated += MergeSpecificIdViewModel_Activated;
            Deactivated += MergeSpecificIdViewModel_Deactivated;
        }

        const string _detailsDefaultMsg = "Please input the desired Id, then press Enter to show details.";

        #region Properties for the view

        private string _changesetDetails;
        public string ChangesetDetails
        {
            get => _changesetDetails;
            set
            {
                if (value != _changesetDetails)
                {
                    _changesetDetails = value;
                    NotifyOfPropertyChange(() => ChangesetDetails);
                }
            }
        }

        private string _id;
        public string Id
        {
            get => _id;
            set
            {
                if (value != _id)
                {
                    _id = value;
                    NotifyOfPropertyChange(() => Id);
                }
            }
        }

        public string BranchSequence
        {
            get
            {
                if (BranchList.Count > 1)
                {
                    var sb = new StringBuilder();
                    string lastItem = BranchList.Last().Name;
                    for (int ii = 0; ii < BranchList.Count; ii++)
                    {
                        sb.Append(BranchList[ii].Name);
                        if (ii < BranchList.Count - 1)
                        {
                            sb.Append(" > ");
                        }
                    }
                    return sb.ToString();
                }
                return string.Empty;
            }
        }

        #endregion

        #region Screen implementation

        private void MergeSpecificIdViewModel_Activated(object sender, ActivationEventArgs e)
        {
            Shell = IoC.Get<IShell>();
            ResetCurrentChangesetInfo();
            SetTfsBranches();
        }

        private void MergeSpecificIdViewModel_Deactivated(object sender, DeactivationEventArgs e)
        {
            BranchList.Clear();
        }

        #endregion

        #region Branche selection

        private void SetTfsBranches()
        {
            if (BranchList.Count < 2)
                throw new InvalidOperationException("BranchPaths.Count");

            TfsConnection.SetLocalPath(BranchType.Source, BranchList[0].FullName);
            TfsConnection.SetLocalPath(BranchType.Target, BranchList[1].FullName);
        }

        #endregion

        private void ResetCurrentChangesetInfo(bool setId = true)
        {
            ChangesetDetails = _detailsDefaultMsg;
            _changesetId = 0;
            if (setId)
                Id = string.Empty;
        }

        public void GotoConnectionSetup()
        {
            EventAggregator.PublishOnUIThread(new ChangeMainModeEvent(MainMode.ConnectionSetup));
        }

        private int _changesetId = 0;

        public void ExecuteFilterViewForId(Key key, string text)
        {
            if (key == Key.Enter)
            {
                int changesetId;
                if (int.TryParse(text, out changesetId))
                {
                    if (changesetId > 0)
                        GetChangeSetDetails(changesetId);
                }
                else
                    ResetCurrentChangesetInfo(false);
            }
        }

        public void GetChangeSetDetails(int id)
        {
            Changeset changeset = TfsConnection.GetChangeset(id);
            if (changeset == null)
                ChangesetDetails = "Changeset #" + id + " not found.";
            else
            {
                var detailsSb = new StringBuilder();
                var nl = Environment.NewLine;
                detailsSb.Append("Id: " + changeset.ChangesetId.ToString("D"));
                detailsSb.Append(nl + nl);
                detailsSb.Append("Date: " + changeset.CreationDate.ToShortDateString());
                detailsSb.Append(nl + nl);
                detailsSb.Append("Committer: " + changeset.OwnerDisplayName);
                detailsSb.Append(nl + nl);
                var comment = changeset.Comment;
                detailsSb.Append("Comment: ");
                if (!string.IsNullOrEmpty(comment))
                    detailsSb.Append(comment);
                ChangesetDetails = detailsSb.ToString();
                _changesetId = changeset.ChangesetId;
            }
        }

        /*public void EditBrances()
        {
            var pathList = _branchList.Select(item => item.FullName).ToList();
            var newPathList = Popups.AskBranchSequence(pathList);
            _branchList.Clear();
            foreach (var path in newPathList)
            {
                var dInfo = new DirectoryInfo(path);
                _branchList.Add(dInfo);
            }
            NotifyOfPropertyChange(() => BranchSequence);
        }*/

        public void ReverseBranches()
        {
            BranchList.Reverse();
            NotifyOfPropertyChange(() => BranchSequence);
        }

        /*
        public async void MergeChangeset()
        {
            if (_changesetId > 0)
            {
                var reporter = Shell.ProgressReporter;
                var numMerges = BranchList.Count - 1;
                Shell.InitAndShowProgress(0, 0, 2 * numMerges, "Merging, please wait");

                var success = true;
                var finishedItems = new List<Tuple<int, int>>();
                var finishedProgress = new Progress<Tuple<int, int>>((item) =>
                {
                    finishedItems.Add(item);
                    Id = item.Item2.ToString();
                });


                for (int ii = 0; ii < numMerges; ii++)
                {
                    var changeset = TfsConnection.GetChangeset(_changesetId);
                    if (changeset != null)
                    {
                        var sourceBranch = BranchList[ii];
                        var targetBranch = BranchList[ii + 1];
                        TfsConnection.SetLocalPath(BranchType.Source, sourceBranch.FullName);
                        TfsConnection.SetLocalPath(BranchType.Target, targetBranch.FullName);

                        var changesetAsList = new List<Changeset>() { changeset };
                        Tuple<bool, string> mergeResult = await Task.Run(() => MergingHelper.DoMerging(TfsConnection, changesetAsList, MergingHelper.CheckinOptions.AfterEachMerge,
                                                                                                            reporter, finishedProgress));
                        if (!mergeResult.Item1)
                        {
                            //Popups.ShowMessage("There was a conflict when merging changeset " + _changesetId);
                            Shell.SetFinalMessage("Error", "Error with changeset " + _changesetId + ". Operation aborted.\n\n" + mergeResult.Item2);
                            success = false;
                            break;
                        }
                    }
                    else
                    {
                        Popups.ShowMessage("Error getting changeset information for id " + _changesetId + ". Operation aborted.");
                        Shell.ProgressIsShown = false;
                        success = false;
                        break;
                    }
                    Debug.Assert(finishedItems.Count == ii + 1);
                    _changesetId = finishedItems.Last().Item2;
                }

                if (success)
                {
                    string finalMessage = "Successfully merged changesets: ";
                    foreach (var item in finishedItems)
                    {
                        finalMessage = finalMessage + "\n  " + item.Item1.ToString() + " in checkin " + item.Item2.ToString();
                    }

                    Shell.SetFinalMessage("Merging done", finalMessage);
                    ResetCurrentChangesetInfo();
                }
            }

        }
        */

    }
}

using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using TFSMergingTool.OutputWindow;
using TFSMergingTool.Resources;
using TFSMergingTool.Settings;
using TFSMergingTool.Shell;
using System.IO;
using Microsoft.TeamFoundation.VersionControl.Client;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.ComponentModel;
using Microsoft.TeamFoundation.VersionControl.Common;
using TFSMergingTool.src.Merging.Options;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace TFSMergingTool.Merging
{
    public interface IMergeFromListViewModel : IScreen
    {
        List<DirectoryInfo> BranchList { get; set; }
    }

    public class ChangesetsMergedEventArgs
    {
        public ChangesetsMergedEventArgs(IList<Changeset> changesets)
        {
            Changesets = changesets;
        }
        public IList<Changeset> Changesets;
    }

    [Export(typeof(IMergeFromListViewModel))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class MergeFromListViewModel : Screen, IMergeFromListViewModel
    {
        public List<DirectoryInfo> BranchList { get; set; }
        IEventAggregator EventAggregator { get; set; }
        IOutputWindow Output { get; set; }
        UserSettings Settings { get; set; }
        MyTfsConnection TfsConnection { get; set; }
        IShell Shell { get; set; }
        IPopupService Popups { get; set; }

        [ImportingConstructor]
        public MergeFromListViewModel(IEventAggregator eventAggregator, IOutputWindow output, UserSettings settings, MyTfsConnection tfsConnection,
            IPopupService popups)
        {
            EventAggregator = eventAggregator;
            Output = output;
            Settings = settings;
            TfsConnection = tfsConnection;
            Popups = popups;
            //Shell = shell;

            CandidateList = new BindableCollection<CandidateListItem>();

            CandidateListView = System.Windows.Data.CollectionViewSource.GetDefaultView(CandidateList);

            Activated += MergingMainViewModel_Activated;
            Deactivated += MergingMainViewModel_Deactivated;

            MaxItemCount = 100;
            GetWorkItemData = true;
        }
        public void GotoConnectionSetup()
        {
            EventAggregator.PublishOnUIThread(new ChangeMainModeEvent(MainMode.ConnectionSetup));
        }

        #region Screen implementation

        private void MergingMainViewModel_Activated(object sender, ActivationEventArgs e)
        {
            Shell = IoC.Get<IShell>();
            NotifyOfPropertyChange(() => BranchSequence);
            CandidateList.Clear();
            NotifyOfPropertyChange(() => NumCandidates);
            SetTfsBranches();
            FilterText = string.Empty;

            _filterTextChangeTimer = new System.Timers.Timer(500);
            _filterTextChangeTimer.AutoReset = true;
            _filterTextChangeTimer.Elapsed += _filterTextChangeTimer_Elapsed;

            if (CurrentOptions == null) CurrentOptions = new MyOptions();
            else CurrentOptions.SetDefaultOptions();
            Refresh();
        }

        private void MergingMainViewModel_Deactivated(object sender, DeactivationEventArgs e)
        {
            BranchList.Clear();
            CandidateList.Clear();

            _filterTextChangeTimer?.Dispose();
            _filterTextChangeTimer = null;
        }

        #endregion

        #region Branch selection

        public string BranchSequence
        {
            get
            {
                if (BranchList.Count <= 1) return string.Empty;

                var sb = new StringBuilder();
                string lastItem = BranchList.Last().Name;
                for (int ii = 0; ii < BranchList.Count; ii++)
                {
                    sb.Append(BranchList[ii].Name);
                    if (ii < BranchList.Count - 1)
                        sb.Append(" > ");
                }
                return sb.ToString();
            }
        }

        public void ReverseBranches()
        {
            BranchList.Reverse();
            SetTfsBranches();
            NotifyOfPropertyChange(() => BranchSequence);
            CandidateList.Clear();
        }

        /// <summary>
        /// Sets the first two items in BranchList to TfsConnection as Source & Target.
        /// </summary>
        private void SetTfsBranches()
        {
            if (BranchList.Count < 2)
                throw new InvalidOperationException("BranchList.Count < 2");

            TfsConnection.SetLocalPath(BranchType.Source, BranchList[0].FullName);
            TfsConnection.SetLocalPath(BranchType.Target, BranchList[1].FullName);
        }

        #endregion

        #region Candidates / Changesets list

        private bool _getWorkItemData;
        public bool GetWorkItemData
        {
            get => _getWorkItemData;
            set
            {
                if (value == _getWorkItemData) return;
                _getWorkItemData = value;
                NotifyOfPropertyChange(() => GetWorkItemData);
            }
        }

        public int NumCandidates => CandidateList.Count;

        public int MaxItemCount { get; set; }

        private IObservableCollection<CandidateListItem> _candidateList;
        public IObservableCollection<CandidateListItem> CandidateList
        {
            get => _candidateList;
            set
            {
                _candidateList = value;
                NotifyOfPropertyChange(() => CandidateList);
                NotifyOfPropertyChange(() => NumCandidates);
            }
        }

        /// <summary>
        /// Currently selected changesets, ordered by ChangesetId.
        /// </summary>
        private IOrderedEnumerable<Changeset> SelectedCandidatesById
            => CandidateList.Where(c => c.IsSelected).Select(c => c.Changeset).OrderBy(cs => cs.ChangesetId);

        /// <summary>
        /// Makes sure that IsSelected == false for all changesets in CandidateList.
        /// </summary>
        private void DeselectAllCandidates()
        {
            foreach (var candidate in CandidateList)
            {
                if (candidate.IsSelected) candidate.IsSelected = false;
            }
        }

        private ICollectionView _candidateListView;

        public ICollectionView CandidateListView
        {
            get => this._candidateListView;
            set
            {
                if (value == this._candidateListView) return;
                this._candidateListView = value;
                NotifyOfPropertyChange(() => CandidateListView);
            }
        }

        public enum ListType
        {
            MergeCandidates,
            Changesets,
        }

        public void RefreshCandidates()
        {
            SetTfsBranches();
            RefreshCandidateList(ListType.MergeCandidates);
        }

        public void RefreshChangesets()
        {
            SetTfsBranches();
            RefreshCandidateList(ListType.Changesets);
        }

        public async void RefreshCandidateList(ListType listType)
        {
            if (MaxItemCount <= 0)
            {
                Popups.ShowMessage("Please set maximum item count to a positive integrer.", MessageBoxImage.Exclamation);
                return;
            }

            CandidateList.Clear();

            var reporter = Shell.ProgressReporter;
            int progressMax = BranchList.Count + 1; // update brances, get candidates (populate list will have its own Max)
            CancellationToken cancelToken = Shell.InitAndShowProgress(0, 0, progressMax, "Get");

            try
            {
                await Task.Run(() => UpdateBranches(TfsConnection, reporter, cancelToken), cancelToken);
                switch (listType)
                {
                    case ListType.MergeCandidates:
                        {
                            var candidateList = await Task.Run(() => RefreshCandidates(TfsConnection, reporter, cancelToken), cancelToken);
                            CandidateList.AddRange(candidateList);
                            break;
                        }
                    case ListType.Changesets:
                        {
                            var candidateList = await Task.Run(() => RefreshChangesets(TfsConnection, reporter, cancelToken), cancelToken);
                            CandidateList.AddRange(candidateList);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(listType), listType, null);
                }
                Shell.ProgressIsShown = false;
            }
            catch (OperationCanceledException)
            {
                Shell.SetFinalMessage(Shell.ProgressTitle + " (Cancelled)", "Stopped after: " + Shell.ProgressState, false);
            }
            catch (Exception ex)
            {
                Popups.ShowMessage(ex.ToString(), MessageBoxImage.Error);
                Shell.ProgressIsShown = false;
            }

            Refresh();
        }

        public bool UpdateBranches(MyTfsConnection tfsConnection, IProgress<ProgressReportArgs> reporter, CancellationToken cancelToken)
        {
            bool retval = true;
            // 1. Update branches.
            for (int ii = 0; ii < BranchList.Count; ii++)
            {
                cancelToken.ThrowIfCancellationRequested();

                var branch = BranchList[ii];
                if (ii == 0)
                    reporter?.Report(new ProgressReportArgs(0, null, "Updating source branch (" + branch.Name + ")..."));
                else
                    reporter?.Report(new ProgressReportArgs(1, null, "Updating target #" + ii.ToString() + " branch (" + branch.Name + ")..."));
                IList<Conflict> getConflicts = tfsConnection.UpdateBranch(branch.FullName);
                retval = !getConflicts.Any();
            }
            return retval;
        }

        public IEnumerable<CandidateListItem> RefreshCandidates(MyTfsConnection tfsConnection, IProgress<ProgressReportArgs> reporter, CancellationToken cancelToken)
        {
            string sourceName = BranchList[0].Name;
            string targetName = BranchList[1].Name;

            reporter?.Report(new ProgressReportArgs(0, null, $"Getting merge candidates from {sourceName} to {targetName} (external call)..."));

            MergeCandidate[] candidateList = tfsConnection.GetMergeCandidates().ToArray();

            cancelToken.ThrowIfCancellationRequested();

            int newMaximumValue = candidateList.Length + 1; // .Count() is a fast operation here
            reporter?.Report(new ProgressReportArgs(1, null, "Populating candidate list...", newMaximumValue));

            var items = new List<CandidateListItem>();
            foreach (var tfsCandidate in candidateList)
            {
                reporter?.Report(new ProgressReportArgs(1, null, null));
                const bool isSelected = false;
                var candidateItem = new CandidateListItem(tfsCandidate.Changeset, tfsCandidate.Partial, isSelected, GetWorkItemData, tfsConnection);
                items.Insert(0, candidateItem);
            }

            return items;
        }

        public IEnumerable<CandidateListItem> RefreshChangesets(MyTfsConnection tfsConnection, IProgress<ProgressReportArgs> reporter, CancellationToken cancelToken)
        {
            reporter.Report(new ProgressReportArgs(0, null, "Getting change sets (external call)..."));

            IEnumerable<Changeset> changesetList = tfsConnection.GetHistory(BranchList.First().FullName);

            cancelToken.ThrowIfCancellationRequested();

            // Cannot do changesetList.Count() here because it'll take a very long time. Estimating with MaxItemCount.
            int newMaximumValue = MaxItemCount + 1;
            reporter?.Report(new ProgressReportArgs(0, null, "Populating changeset list...", newMaximumValue));

            var items = new List<CandidateListItem>();
            var count = 0;
            foreach (var cs in changesetList)
            {
                reporter?.Report(new ProgressReportArgs(1, null, null));
                const bool isSelected = false;
                const bool isPartial = false;
                var candidateItem = new CandidateListItem(cs, isPartial, isSelected, GetWorkItemData, tfsConnection);
                //items.Insert(0, candidateItem);
                items.Add(candidateItem);

                /* TFS documentation says its as effective to limit consumption of these
                 * items as it would be to only get a certain amount of them initially. */
                count++;
                if (count >= MaxItemCount)
                    break;
            }

            return items;
        }

        #endregion

        #region Candidate list filtering

        private string _filterText;

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (value != _filterText)
                {
                    _filterText = value;
                    NotifyOfPropertyChange(() => FilterText);
                    BeginProcessingFilterTextChange(string.IsNullOrEmpty(value));
                }
            }
        }

        System.Timers.Timer _filterTextChangeTimer;
        private void BeginProcessingFilterTextChange(bool immediate)
        {
            if (!immediate)
            {
                // If the timer is already running, reset it.
                if (_filterTextChangeTimer.Enabled)
                {
                    _filterTextChangeTimer.Enabled = false;
                }
                _filterTextChangeTimer.Enabled = true;
            }
            else
            {
                ProcessFilterTextChange();
            }
        }

        private void _filterTextChangeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _filterTextChangeTimer.Enabled = false;
            Caliburn.Micro.Execute.OnUIThread(new System.Action(() => ProcessFilterTextChange()));
        }

        private void ProcessFilterTextChange()
        {
            if (string.IsNullOrEmpty(FilterText))
            {
                CandidateListView.Filter = null;
            }
            else
            {
                if (CandidateListView.Filter == null)
                {
                    CandidateListView.Filter = new Predicate<object>(ShouldCandidateListItemBeShown);
                }
                else CandidateListView.Refresh();
            }
        }

        /// <summary>
        /// Returns true if the item should be shown, based on the current filter criteria.
        /// </summary>
        /// <param name="itemObject"></param>
        /// <returns></returns>
        public bool ShouldCandidateListItemBeShown(object itemObject)
        {
            if (string.IsNullOrEmpty(FilterText) != false) return true;

            var retval = true;
            Debug.Assert(itemObject is CandidateListItem);
            if (itemObject is CandidateListItem item)
            {
                // note: If selected items are not shown, then selecting another item will not select the items not shown!
                retval = item.IsSelected || item.Changeset.Comment.IndexOf(FilterText, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }
            return retval;
        }

        public void ClearFilter() => FilterText = string.Empty;

        #endregion

        #region Action guard properties

        void RefreshButtonCanProperties()
        {
            NotifyOfPropertyChange(() => CanMergeIndividually);
        }

        // TBD: Need a SelectionChanged event or smt from the list.
        //public bool CanMergeIndividually { get { return CandidateList.Any(item => item.IsSelected); } }
        public bool CanMergeIndividually => true;

        #endregion

        #region Merging options

        private MyOptions CurrentOptions { get; set; }

        // Merging options

        public bool OptionDiscardIsChecked
        {
            get => CurrentOptions.Discard;
            set
            {
                if (value == CurrentOptions.Discard) return;
                CurrentOptions.Discard = value;
                NotifyOfPropertyChange(() => OptionDiscardIsChecked);
            }
        }

        public bool OptionForceIsChecked
        {
            get => CurrentOptions.Force;
            set
            {
                if (value == CurrentOptions.Force) return;
                CurrentOptions.Force = value;
                NotifyOfPropertyChange(() => OptionForceIsChecked);
            }
        }

        public bool OptionBaselessIsChecked
        {
            get => CurrentOptions.Baseless;
            set
            {
                if (value == CurrentOptions.Baseless) return;
                CurrentOptions.Baseless = value;
                NotifyOfPropertyChange(() => OptionBaselessIsChecked);
            }
        }

        public bool OptionUseRangeIsChecked
        {
            get => CurrentOptions.UseRange;
            set
            {
                if (value == CurrentOptions.UseRange) return;
                CurrentOptions.UseRange = value;
                NotifyOfPropertyChange(() => OptionUseRangeIsChecked);
            }
        }

        // Checkin options

        public bool CheckinNoCheckinIsChecked
        {
            get => CurrentOptions.CheckinOptions.HasFlag(MyCheckinOptions.NoCheckin);
            set
            {
                const MyCheckinOptions optionAsEnum = MyCheckinOptions.NoCheckin;
                if (value == false)
                {
                    if (CurrentOptions.CheckinOptions == optionAsEnum)
                    {
                        // There were no other flags -> replace with default value.
                        CurrentOptions.CheckinOptions = MyCheckinOptions.None;
                    }
                    else if (CurrentOptions.CheckinOptions.HasFlag(optionAsEnum))
                    {
                        // Remove Flag value.
                        CurrentOptions.CheckinOptions &= ~optionAsEnum;
                    }
                }
                else
                {
                    // Add Flag value.
                    CurrentOptions.CheckinOptions |= optionAsEnum;
                }
                NotifyOfPropertyChange(() => CheckinNoCheckinIsChecked);
            }
        }

        #endregion

        #region Merge functions

        public void StartMergeOneByOne()
        {
            MergeOptionsEx mergeOptions = CurrentOptions.MergeOptions;
            MyCheckinOptions checkinOptions = CurrentOptions.CheckinOptions;

            // Discards are typically cleanup operations, so don't update work item history for those.
            bool doAssociateWorkItems = mergeOptions.HasFlag(MergeOptionsEx.AlwaysAcceptMine) == false;
            DoMergeOneByOne(mergeOptions, checkinOptions, doAssociateWorkItems);
            NotifyOfPropertyChange(() => NumCandidates);
        }

        public void StartMergeRange()
        {
            MergeOptionsEx mergeOptions = CurrentOptions.MergeOptions;
            MyCheckinOptions checkinOptions = CurrentOptions.CheckinOptions;

            if (BranchList.Count != 2)
            {
                Popups.ShowMessage("Combining multiple changesets in one merge operation is currently " +
                                   "only supported for one target branch (merge chain length of 1).",
                                    MessageBoxImage.Exclamation);
            }
            else
            {
                // Discards are typically cleanup operations, so don't update work item history for those.
                bool doAssociateWorkItems = mergeOptions.HasFlag(MergeOptionsEx.AlwaysAcceptMine) == false;
                DoMergeRange(mergeOptions, checkinOptions, doAssociateWorkItems);
                NotifyOfPropertyChange(() => NumCandidates);
            }
        }

        private async void DoMergeOneByOne(MergeOptionsEx mergeOptions, MyCheckinOptions checkinOptions, bool associateWorkItems)
        {
            Changeset[] changesets = SelectedCandidatesById.ToArray();
            if (changesets.Any())
            {
                var mergeDepth = BranchList.Count - 1;
                var numMerges = changesets.Length * mergeDepth;
                int maxProgress = 2 * numMerges;

                CancellationToken cancelToken = Shell.InitAndShowProgress(0, 0, maxProgress, "Merging, please wait");
                IProgress<ProgressReportArgs> reporter = Shell.ProgressReporter;

                var finishedItems = new List<FinishedItemReport>();
                var finishedProgress = new Progress<FinishedItemReport>((item) =>
                {
                    finishedItems.Add(item);

                    if (CandidateList.Any(it => it.Changeset.ChangesetId == item.SourceChangesetId))
                    {
                        // Clean completed items from UI list
                        var match = CandidateList.First(it => it.Changeset.ChangesetId == item.SourceChangesetId);
                        CandidateList.Remove(match);
                    }
                });

                try
                {
                    bool doCheckin = checkinOptions.HasFlag(MyCheckinOptions.NoCheckin) == false;

                    Tuple<bool, string> mergeResult = await Task.Run(()
                        => MergingHelper.MergeAndCommitOneByOne(TfsConnection, changesets, BranchList, reporter, finishedProgress,
                            cancelToken, Settings.TfsExecutable, Popups, mergeOptions, doCheckin, associateWorkItems), cancelToken);

                    if (mergeResult.Item1 == true)
                    {
                        Debug.Assert(finishedItems.Count == numMerges);

                        var sb = new StringBuilder();
                        sb.Append("Successfully merged " + finishedItems.Count + " changesets:\n");
                        foreach (var item in finishedItems)
                        {
                            sb.Append("  ");
                            for (int ii = 0; ii < item.SourceBranchIndex; ii++) sb.Append("  ");
                            sb.Append(item.CommitChangesetId.ToString() + " : " + "\"" + item.CommitComment + "\"\n");
                        }

                        Shell.SetFinalMessage("Merging done and changes checked in", sb.ToString());
                    }
                    else
                    {
                        Shell.SetFinalMessage(null, null, false);
                    }
                }
                catch (OperationCanceledException)
                {
                    Shell.SetFinalMessage(Shell.ProgressTitle + " (Cancelled)", "Stopped after: " + Shell.ProgressState, false);
                }
                catch (Exception ex)
                {
                    Popups.ShowMessage(ex.ToString(), MessageBoxImage.Error);
                    Shell.ProgressIsShown = false;
                }
            }
        }

        private async void DoMergeRange(MergeOptionsEx mergeOptions, MyCheckinOptions checkinOptions, bool associateWorkItems)
        {
            Changeset[] changesets = SelectedCandidatesById.ToArray();
            if (changesets.Length > 0)
            {
                bool sequential = CheckIfChangesetIdsAreSequential(changesets);
                if (!sequential)
                {
                    int firstId = changesets.First().ChangesetId;
                    int lastId = changesets.Last().ChangesetId;
                    var answer = Popups.AskYesNoQuestion("The selected changesets do not have consecutive ids." +
                                                          $"\nDo you want to merge ALL changesets from the source branch between ids {firstId} and {lastId}?");

                    Shell.SetFinalMessage(Shell.ProgressTitle + " (Cancelled)", "User cancellation", false);
                    if (answer != MessageBoxResult.Yes) return;
                }

                var mergeDepth = BranchList.Count - 1;
                Debug.Assert(mergeDepth == 1, "DoMergeRange: the only supported mergeDepth is 1");

                var numMerges = changesets.Count() * mergeDepth;

                CancellationToken cancelToken = Shell.InitAndShowProgress(0, 0, numMerges + 1, "Merging, please wait");
                IProgress<ProgressReportArgs> reporter = Shell.ProgressReporter;

                try
                {
                    Tuple<bool, string> mergeResult = await Task.Run(()
                        => MergingHelper.MergeRange(TfsConnection, changesets, BranchList, reporter, cancelToken,
                            Settings.TfsExecutable, Popups, mergeOptions), cancelToken);

                    if (mergeResult.Item1 == true)
                    {
                        bool doCheckin = checkinOptions.HasFlag(MyCheckinOptions.NoCheckin) == false;
                        bool checkinSuccess = false;

                        if (doCheckin)
                        {
                            var idAndOwnerOfChanges = changesets.Select(cs => Tuple.Create(cs.ChangesetId, cs.OwnerDisplayName)).ToArray();

                            string defaultComment = CommentBuilder.GetCombinedMergeCheckinComment(
                                BranchList[0].Name, BranchList[1].Name, idAndOwnerOfChanges, mergeOptions);

                            checkinSuccess = CheckInAtTheEndOfMerging(changesets, associateWorkItems, defaultComment);
                        }

                        if (checkinSuccess == true || doCheckin == false)
                        {
                            // Clean completed items from UI list
                            foreach (var cs in changesets)
                            {
                                var match = CandidateList.First(it => it.Changeset.ChangesetId == cs.ChangesetId);
                                CandidateList.Remove(match);
                            }
                        }

                        if (checkinSuccess == false || doCheckin == false)
                        {
                            Shell.SetFinalMessage("Changes not checked in", "All selected items were merged on disk, but the changes were not checked in.\n\n");
                        }
                        else
                        {
                            Shell.SetFinalMessage("Merging done.", $"Successfully merged {changesets.Length} changesets, and checked in the changes.");
                        }
                    }
                    else
                    {
                        Shell.SetFinalMessage(null, null, false);
                    }
                }
                catch (OperationCanceledException)
                {
                    Shell.SetFinalMessage(Shell.ProgressTitle + " (Cancelled)", "Stopped after: " + Shell.ProgressState, false);
                }
                catch (Exception ex)
                {
                    Popups.ShowMessage(ex.ToString(), MessageBoxImage.Error);
                    Shell.ProgressIsShown = false;
                }
            }
        }

        /// <summary>
        /// Returns true if the given changesets have consecutive ids.
        /// </summary>
        private bool CheckIfChangesetIdsAreSequential(Changeset[] changesets)
        {
            bool sequential = changesets.Zip(changesets.Skip(1), (a, b) => (a.ChangesetId + 1) == b.ChangesetId).All(x => x);
            return sequential;
        }

        /// <summary>
        /// Used after merging a range of changeset in one operation.
        /// </summary>
        private bool CheckInAtTheEndOfMerging(Changeset[] changesets, bool associateWorkItems, string defaultComment)
        {
            var items = changesets.Select(cs => new FinishedItemReport()
            {
                SourceChangesetId = cs.ChangesetId,
                SourceBranchIndex = 0,
                CommitChangesetId = -1,
                CommitComment = cs.Comment
            }).ToArray();

            return CheckInAtTheEndOfMerging(items, associateWorkItems, defaultComment);
        }

        /// <summary>
        /// Used after merging selected changeset in sequential merge operations.
        /// </summary>
        private bool CheckInAtTheEndOfMerging(IList<FinishedItemReport> finishedItems, bool associateWorkItems, string defaultComment)
        {
            bool success = true;

            var workItemsToAssociate = new List<Microsoft.TeamFoundation.WorkItemTracking.Client.WorkItem>();
            if (associateWorkItems)
            {
                foreach (var item in finishedItems)
                {
                    if (item.SourceBranchIndex != 0) continue;

                    Changeset cs = TfsConnection.GetChangeset(item.SourceChangesetId);
                    foreach (var wi in cs.WorkItems)
                    {
                        if (!workItemsToAssociate.Contains(wi))
                            workItemsToAssociate.Add(wi);
                    }
                }
            }

            PendingChange[] pendingChanges = TfsConnection.GetPendingChanges();

            string answer = Popups.AskStringInput($"There are {pendingChanges.Count()} pending changes.\nPlease provide the checkin comment.", defaultComment);
            if (string.IsNullOrEmpty(answer))
            {
                success = false;
            }
            else
            {
                TfsConnection.Checkin(answer, workItemsToAssociate.ToArray());
            }

            return success;
        }

        #endregion

        #region Context menu items

        public void MenuShowDetails(object source)
        {
            var item = (CandidateListItem)source;
            Popups.ShowMessage(item.Changeset.Comment);
        }

        public void MenuSelectedToClipboard()
        {
            CandidateListItem[] candidates = CandidateList
                .Where(c => c.IsSelected)
                .OrderBy(it => it.Changeset.ChangesetId)
                .ToArray();

            if (!candidates.Any()) return;

            var sb = new StringBuilder();
            const string separator = " | ";
            sb.AppendLine("Changeset Id" + separator + "Comment" + separator + "Associated Work Items" + separator + "Main Work Item");
            foreach (var candidate in candidates)
            {
                Changeset change = candidate.Changeset;
                if (change == null) continue;

                string workItems = WorkItemHelper.WorkItemsToString(change.WorkItems);
                sb.Append(change.ChangesetId.ToString() + separator);
                sb.Append(change.Comment.Trim() + separator);
                if (!string.IsNullOrEmpty(workItems)) sb.Append(workItems + separator);

                if (candidate.WiProperties != null)
                {
                    sb.Append(candidate.WiProperties.Id.ToString() + " ");
                    if (!string.IsNullOrEmpty(candidate.WiProperties.State)) sb.Append(candidate.WiProperties.State + ": ");
                    if (!string.IsNullOrEmpty(candidate.WiProperties.Title)) sb.Append(candidate.WiProperties.Title + " ");
                }

                sb.Append(Environment.NewLine);
            }
            //Popups.ShowMessage(sb.ToString());
            //System.Diagnostics.Process.Start("http://google.com");
            System.Windows.Clipboard.SetText(sb.ToString());
        }

        public void MenuCommentToClipboard(object source)
        {
            var item = (CandidateListItem)source;
            System.Windows.Clipboard.SetText(item.Changeset.Comment);
        }

        public void MenuOpenSelectedInBrowser(object source)
        {
            switch (source)
            {
                case CandidateListItem candidate:
                    {
                        // Opens the selected item in the browser.
                        var wiProperties = candidate.WiProperties;
                        WorkItem wi = wiProperties?.WorkItemObject;
                        if (wi != null)
                        {
                            WorkItemHelper.OpenWorkItemInBrowser(wi);
                        }

                        break;
                    }
                case System.Windows.Input.KeyEventArgs keyArgs:
                    {
                        // Opens all selected items in the browser.
                        if (keyArgs.Key == System.Windows.Input.Key.B &&
                            keyArgs.IsDown &&
                            System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl))
                        {
                            var candidates = CandidateList.Where(c => c.IsSelected).OrderBy(it => it.Changeset.ChangesetId);
                            foreach (var item in candidates)
                                MenuOpenSelectedInBrowser(item);
                        }

                        break;
                    }
            }
        }

        public void MenuEditWorkItemFields()
        {
            CandidateListItem[] candidates = CandidateList.Where(c => c.IsSelected).OrderBy(it => it.Changeset.ChangesetId).ToArray();
            if (!candidates.Any()) return;

            string fieldNameToModify = Popups.AskStringInput("Which Field do you want to modify?", "Planned Release");
            if (string.IsNullOrEmpty(fieldNameToModify) == false)
            {
                WorkItem firstWorkItem = candidates.First().WiProperties.WorkItemObject;
                Field field = WorkItemHelper.GetFieldIfExists(firstWorkItem, fieldNameToModify);
                Popups.ShowMessage(field == null
                    ? $"Could not find Field {fieldNameToModify} on the 1st Work Item ({firstWorkItem.Id})"
                    : $"Success: field {fieldNameToModify} found!");
            }
        }

        #endregion

        public void ShowSelection()
        {
            Changeset[] changesets = SelectedCandidatesById.ToArray();
            if (changesets.Any())
            {
                var sb = new StringBuilder();
                foreach (var item in changesets)
                {
                    string comment = item.Comment;
                    comment = comment.Length <= 50 ? comment : comment.Substring(0, 50 - 3) + "...";
                    sb.AppendLine($"+ {item.ChangesetId} : {comment}");
                }

                Popups.ShowMessage($"{changesets.Count()} changesets selected:" + Environment.NewLine + sb);
            }
            else
            {
                Popups.ShowMessage("No changesets selected.");
            }
        }
    }
}

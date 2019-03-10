using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TFSMergingTool.Resources;
using TFSMergingTool.Shell;

namespace TFSMergingTool.Merging
{
    public class FinishedItemReport
    {
        public int SourceChangesetId { get; set; }
        public int CommitChangesetId { get; set; }
        public string CommitComment { get; set; }
        public int SourceBranchIndex { get; set; }
    }

    public static class MergingHelper
    {
        public enum CheckinOptions
        {
            NoCheckIn,
            AfterEachMerge,
            AtTheEnd
        }

        private static WorkItem[] GetRelatedWorkItems(IEnumerable<Changeset> changesets)
        {
            return changesets.SelectMany(cs => cs.WorkItems).ToArray();
        }

        /// <summary>
        /// Merges the changesets one by one. Each of the changesets is merged through all branches, ie. from first to last.
        /// </summary>
        /// <returns>A tuple, where the boolean signals success, and the string contains and error message in case of failure.</returns>
        public static Tuple<bool, string> MergeAndCommitOneByOne(MyTfsConnection tfsConnection, Changeset[] changesets,
            IList<DirectoryInfo> branches, IProgress<ProgressReportArgs> reporter, IProgress<FinishedItemReport> finishedItem,
            CancellationToken cancelToken, FileInfo tfExecutable, IPopupService popupService,
            MergeOptionsEx mergeOptions = MergeOptionsEx.None, bool doCheckin = true, bool associateWorkItems = true)
        {
            if (!changesets.Any() || branches.Count <= 1)
                return Tuple.Create(false, "No changesets, or not >= 2 branches.");

            if (doCheckin == false)
            {
                Debug.Assert(branches.Count == 2, "Currently not supported to have more than two branches in the chain when not checkin in after each merge.");
            }

            // For the whole length of the list of changeset in the source branch.
            foreach (var csOriginal in changesets)
            {
                cancelToken.ThrowIfCancellationRequested();

                // For the whole depth of the list of branch merge chain.
                var csCurrent = tfsConnection.GetChangeset(csOriginal.ChangesetId);
                int newCheckinId = 0;
                for (int ii = 0; ii < branches.Count - 1; ii++)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (csCurrent != null)
                    {
                        var sourceBranch = branches[ii];
                        var targetBranch = branches[ii + 1];
                        tfsConnection.SetLocalPath(BranchType.Source, sourceBranch.FullName);
                        tfsConnection.SetLocalPath(BranchType.Target, targetBranch.FullName);

                        var id = csCurrent.ChangesetId;
                        string checkinComment = CommentBuilder.GetComment(csOriginal.Comment, id, csOriginal.OwnerDisplayName,
                            sourceBranch.Name, targetBranch.Name, mergeOptions);

                        reporter?.Report(new ProgressReportArgs(0, null, "Merging: " + checkinComment));

                        try
                        {
                            CheckInitialConflicts(tfsConnection, targetBranch, checkinComment);

                            IList<Conflict> conflicts = tfsConnection.Merge(id, id, mergeOptions);

                            cancelToken.ThrowIfCancellationRequested();

                            if (conflicts.Count > 0 && !conflicts.All(c => c.AutoResolved))
                            {
                                //ResolveConflictsWithAPICall(conflicts, tfsConnection, checkinComment);
                                var conflictRetval = ConflictResolver.ResolveConflictsWithExternalExecutable(
                                    tfExecutable, tfsConnection, targetBranch.FullName, popupService, checkinComment);

                                if (conflictRetval.Item1 == false)
                                    throw new MyTfsConflictException(conflictRetval.Item2.Count() + " unresolved conflicts.");

                                cancelToken.ThrowIfCancellationRequested();
                            }

                            if (doCheckin)
                            {
                                reporter?.Report(new ProgressReportArgs(1, null, "Checkin: " + checkinComment));

                                var workItems = associateWorkItems ? csCurrent.WorkItems : new WorkItem[0];
                                workItems = FilterWorkItemsToUpdate(workItems);
                                newCheckinId = tfsConnection.Checkin(checkinComment, workItems);
                            }

                            reporter?.Report(new ProgressReportArgs(1));

                            finishedItem?.Report(new FinishedItemReport()
                            {
                                SourceChangesetId = id,
                                CommitChangesetId = newCheckinId,
                                CommitComment = checkinComment,
                                SourceBranchIndex = ii
                            });

                        }
                        catch (MyTfsConnectionException ex)
                        {
                            reporter?.Report(new ProgressReportArgs(0, "Error", "Error merging changeset: " + id.ToString() + "\n\n" + ex.ToString()));
                            return Tuple.Create(false, ex.ToString());
                        }

                    }
                    else
                    {
                        var errorMsg = "Error getting changeset information for id " + csOriginal.ChangesetId + ". Operation aborted.";
                        reporter?.Report(new ProgressReportArgs(0, "Error", errorMsg));
                        return Tuple.Create(false, errorMsg);
                    }

                    if (newCheckinId > 0) csCurrent = tfsConnection.GetChangeset(newCheckinId);
                }
            }

            return Tuple.Create(true, "Successfully merged.");
        }

        /// <summary>
        /// Picks only those work items that should be updated.
        /// </summary>
        private static WorkItem[] FilterWorkItemsToUpdate(WorkItem[] itemsIn)
        {
            var ret = new List<WorkItem>();
            foreach (var item in itemsIn)
            {
                if (item.IterationPath.StartsWith("Revolution40"))
                {
                    ret.Add(item);
                }
                else
                {
                    var popups = Caliburn.Micro.IoC.Get<IPopupService>();
                    popups.ShowMessage("Skipping work item # " + item.Id + " update because iteration field was " + item.IterationPath);
                }
            }
            return ret.ToArray();
        }

        private static void CheckInitialConflicts(MyTfsConnection tfsConnection, DirectoryInfo targetBranch, string whatAreWeMerging)
        {
            var initialConflicts = tfsConnection.WorkSpace.QueryConflicts(new string[] { targetBranch.FullName }, true);
            if (initialConflicts.Any())
            {
                throw new MyTfsConflictException(initialConflicts.Count() + " unresolved conflict(s) before starting to merge \"" + whatAreWeMerging +
                                                "\"\n\nIn folder " + targetBranch.FullName);
            }
        }

        /// <summary>
        /// Merges a range changesets. Does not perform a check in.
        /// </summary>
        /// <returns>A tuple, where the boolean signals success, and the string contains and error message in case of failure.</returns>
        public static Tuple<bool, string> MergeRange(MyTfsConnection tfsConnection, Changeset[] changesets, IList<DirectoryInfo> branches,
            IProgress<ProgressReportArgs> reporter, CancellationToken cancelToken, FileInfo tfExecutable, IPopupService popupService,
            MergeOptionsEx mergeOptions = MergeOptionsEx.None)
        {
            Debug.Assert(branches.Count == 2, "Merging as range currently only supports two branches (1 source + 1 target)");
            //if (doCheckin == false)
            //{
            //    Debug.Assert(branches.Count == 2, "Currently not supported to have more than two branches in the chain when not checkin in after each merge.");
            //}

            if (changesets.Any() && branches.Count > 1)
            {
                DirectoryInfo sourceBranch = branches[0];
                DirectoryInfo targetBranch = branches[1];
                tfsConnection.SetLocalPath(BranchType.Source, sourceBranch.FullName);
                tfsConnection.SetLocalPath(BranchType.Target, targetBranch.FullName);

                string whatAreWeDoing = $"Merging changesets {changesets.First().ChangesetId} - {changesets.Last().ChangesetId}";
                reporter?.Report(new ProgressReportArgs(0, null, whatAreWeDoing));

                cancelToken.ThrowIfCancellationRequested();

                try
                {
                    CheckInitialConflicts(tfsConnection, targetBranch, whatAreWeDoing);

                    int firstId = changesets.First().ChangesetId;
                    int lastId = changesets.Last().ChangesetId;

                    IList<Conflict> conflicts = tfsConnection.Merge(firstId, lastId, mergeOptions);

                    cancelToken.ThrowIfCancellationRequested();

                    if (conflicts.Count > 0 && !conflicts.All(c => c.AutoResolved))
                    {
                        //ResolveConflictsWithAPICall(conflicts, tfsConnection, checkinComment);
                        var conflictRetval = ConflictResolver.ResolveConflictsWithExternalExecutable(tfExecutable, tfsConnection, targetBranch.FullName, popupService);

                        if (!conflictRetval.Item1)
                            throw new MyTfsConflictException(conflictRetval.Item2.Count() + " unresolved conflicts.");

                        cancelToken.ThrowIfCancellationRequested();
                    }

                    reporter?.Report(new ProgressReportArgs(1));
                }
                catch (MyTfsConnectionException ex)
                {
                    reporter?.Report(new ProgressReportArgs(0, "Error", "Error " + whatAreWeDoing + "\n\n" + ex.ToString()));
                    return Tuple.Create(false, ex.ToString());
                }

                return Tuple.Create(true, "Successfully merged.");
            }

            return Tuple.Create(false, "No changesets, or not >= 2 branches.");
        }
    }
}



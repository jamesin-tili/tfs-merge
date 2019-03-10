using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.Framework.Common;
using System.IO;
using System.ComponentModel.Composition;
using TFSMergingTool.Resources;
using System.Windows;
using TFSMergingTool.OutputWindow;

namespace TFSMergingTool.Resources
{
    class MyTFSConnectionException : Exception
    {
        public MyTFSConnectionException(string message)
            : base(message)
        {
        }
    }

    class MyTFSConflictExeption : MyTFSConnectionException
    {
        public MyTFSConflictExeption(string message)
            : base(message)
        {
        }
    }

    public enum BranchType
    {
        Source,
        Target,
        All
    }

    public enum ConnectionState
    {
        UnknownError,
        Connected,
        AuthorizationError
    }

    public class TeamProjectCollectionData
    {
        public string Name { get; set; }
        public Uri Uri { get; set; }
        public List<string> TeamProjectNames { get; set; }
    }

    internal struct BranchPaths
    {
        public string Local;
        public string Server;
        public BranchPaths(string local, string server)
        {
            Local = local;
            Server = server;
        }
    }

    [Export(typeof(MyTFSConnection))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MyTFSConnection
    {
        public bool IsConnected { get; private set; }
        public Workspace WorkSpace { get; set; }

        private IOutputWindow Output { get; set; }

        private TfsConfigurationServer _configurationServer;
        private TfsTeamProjectCollection _projectCollection;
        private WorkItemStore _workItemStore;
        private VersionControlServer _versionControlServer;
        private Dictionary<BranchType, BranchPaths> _branchMap;

        public event ConflictEventHandler Conflict
        {
            add
            {
                if (_versionControlServer == null)
                    throw new InvalidOperationException("Connect to server before assigning exceptions.");
                _versionControlServer.Conflict += value;
            }
            remove
            {
                if (_versionControlServer != null)
                    _versionControlServer.Conflict -= value;
            }
        }

        public event ResolvedConflictEventHandler ResolvedConflict
        {
            add
            {
                if (_versionControlServer == null)
                    throw new InvalidOperationException("Connect to server before assigning exceptions.");
                _versionControlServer.ResolvedConflict += value;
            }
            remove
            {
                if (_versionControlServer != null)
                    _versionControlServer.ResolvedConflict -= value;
            }
        }

        private IPopupService Popups { get; set; }

        [ImportingConstructor]
        public MyTFSConnection(IPopupService popUpService, IOutputWindow outputWnd)
        {
            Popups = popUpService;
            Output = outputWnd;

            IsConnected = false;
            _branchMap = new Dictionary<BranchType, BranchPaths>()
            {
                {BranchType.Source, new BranchPaths(string.Empty, string.Empty)},
                {BranchType.Target, new BranchPaths(string.Empty, string.Empty)},
                {BranchType.All, new BranchPaths(string.Empty, string.Empty)}
            };
        }

        ~MyTFSConnection()
        {
            Disconnect();
        }

        public void Disconnect()
        {
            if (_projectCollection != null) _projectCollection.Dispose();
            if (_configurationServer != null) _configurationServer.Dispose();
            ClearBranchPaths();
            IsConnected = false;
        }

        public void ClearBranchPaths()
        {
            _branchMap[BranchType.Source] = new BranchPaths(string.Empty, string.Empty);
            _branchMap[BranchType.Target] = new BranchPaths(string.Empty, string.Empty);
        }

        /// <summary>
        /// Transforms a BranchType input into a list that be iterated in a foreach loop.
        /// The list can only be longer than one if the input is BranchType.All
        /// </summary>
        private List<BranchType> BranchToList(BranchType Branch)
        {
            List<BranchType> branches;
            if (Branch == BranchType.All)
            {
                branches = _branchMap.Keys.ToList();
                branches.Remove(BranchType.All);
            }
            else
            {
                branches = new List<BranchType>() { Branch };
            }
            return branches;
        }

        public ConnectionState ConnectToServer(Uri serverUri, out List<TeamProjectCollectionData> teamProjectCollections)
        {
            IsConnected = false;
            teamProjectCollections = new List<TeamProjectCollectionData>();
            try
            {
                _configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(serverUri);

                // Get the catalog of team project collections
                ReadOnlyCollection<CatalogNode> collectionNodes = _configurationServer.CatalogNode.QueryChildren(
                    new[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);

                // List the team project collections
                foreach (CatalogNode collectionNode in collectionNodes)
                {
                    // Use the InstanceId property to get the team project collection
                    Guid collectionId = new Guid(collectionNode.Resource.Properties["InstanceId"]);
                    TfsTeamProjectCollection teamProjectCollection = _configurationServer.GetTeamProjectCollection(collectionId);

                    // Get a catalog of team projects for the collection
                    ReadOnlyCollection<CatalogNode> projectNodes = collectionNode.QueryChildren(
                        new[] { CatalogResourceTypes.TeamProject },
                        false, CatalogQueryOptions.None);

                    // List the team projects in the collection
                    var teamProjectNames = new List<string>();
                    foreach (CatalogNode projectNode in projectNodes)
                    {
                        teamProjectNames.Add(projectNode.Resource.DisplayName);
                    }

                    teamProjectCollections.Add(new TeamProjectCollectionData()
                    {
                        Name = teamProjectCollection.Name,
                        Uri = teamProjectCollection.Uri,
                        TeamProjectNames = teamProjectNames
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                return ConnectionState.AuthorizationError;
            }
            catch (Exception ex)
            {
                //throw new MyTFSConnectionException(" exception on connect:\n" + ex.ToString());
                Popups.ShowMessage("Exception on connect: " + ex.ToString(), MessageBoxImage.Error);
                return ConnectionState.UnknownError;
            }
            IsConnected = true;
            return ConnectionState.Connected;
        }

        public ConnectionState ConnectToTeamProjectCollection(Uri collectionUri, string userName = "", string password = "")
        {
            try
            {
                if (userName != string.Empty)
                {
                    _projectCollection = new TfsTeamProjectCollection(collectionUri, new System.Net.NetworkCredential(userName, password));
                }
                else
                {
                    _projectCollection = new TfsTeamProjectCollection(collectionUri);
                }
                _workItemStore = _projectCollection.GetService<WorkItemStore>();
                _versionControlServer = _projectCollection.GetService<VersionControlServer>();
            }
            catch (Microsoft.TeamFoundation.TeamFoundationServiceUnavailableException ex)
            {
                Popups.ShowMessage("Exception when connecting to team project: " + ex.ToString(), MessageBoxImage.Error);
                return ConnectionState.UnknownError;
            }
            return ConnectionState.Connected;
        }

        public Workspace[] GetWorkspaces()
        {
            Workspace[] workspaces;
            try
            {
                workspaces = _versionControlServer.QueryWorkspaces(null, _versionControlServer.AuthorizedUser, Environment.MachineName);
            }
            catch (Exception ex)
            {
                Popups.ShowMessage("Exception when connecting to team project: " + ex.ToString(), MessageBoxImage.Error);
                workspaces = new Workspace[] { };
            }
            return workspaces;
        }

        public WorkItem GetWorkItem(int Id)
        {
            if (_workItemStore == null)
                throw new InvalidOperationException("Attempt to get work items before connecting.");
            return _workItemStore.GetWorkItem(Id);
        }

        /// <summary>
        /// Get information conserning a specific changeset. Returns null if not found.
        /// </summary>
        public Changeset GetChangeset(int Id)
        {
            if (_versionControlServer == null)
                throw new InvalidOperationException("Attempt to get changesets before connecting.");
            try
            {
                var changeset = _versionControlServer.GetChangeset(Id);
                return changeset;
            }
            catch (ChangesetNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the server item matching the given local item. Returns an empty string if such was not found.
        /// </summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        public string GetServerPathForLocalPath(string localPath)
        {
            if (IsConnected == false) throw new InvalidOperationException("Cannot get folders before connecting to server");
            if (WorkSpace == null) throw new InvalidOperationException("Cannot get folders before setting a workspace");
            if (!WorkSpace.IsLocalPathMapped(localPath))
            {
                return string.Empty;
            }
            try
            {
                string serverPath = WorkSpace.GetServerItemForLocalItem(localPath);
                return serverPath;
            }
            catch (ItemNotMappedException)
            {
                return string.Empty;
            }
        }

        public void SetLocalPath(BranchType branch, string localPath)
        {
            if (branch == BranchType.All)
            {
                throw new MyTFSConnectionException("Cannot set a local directory for BranchType.All");
            }
            if (!Directory.Exists(localPath))
            {
                throw new MyTFSConnectionException("Local directory does not exist: " + localPath);
            }
            if (!WorkSpace.IsLocalPathMapped(localPath))
            {
                throw new MyTFSConnectionException("Local directory " + localPath + " is not mapped in the TFS workspace");
            }

            string serverPath = WorkSpace.GetServerItemForLocalItem(localPath);

            // double check
            string localPathFromWorkspace = WorkSpace.GetLocalItemForServerItem(serverPath);
            if (localPathFromWorkspace != localPath)
            {
                string message = string.Format("When trying to set {0}:\n", branch.ToString());
                message += string.Format("Inconsistent local and server items: local {0}, server {1}, local matching server {2}.",
                    localPath, serverPath, localPathFromWorkspace);
                throw new MyTFSConnectionException(message);
            }

            _branchMap[branch] = new BranchPaths(localPath, serverPath);
        }

        public string GetLocalPath(BranchType branch)
        {
            if (branch == BranchType.All) throw new MyTFSConnectionException("Cannot get the local directory of BranchType.All");
            var paths = _branchMap[branch];

            if (paths.Local.IsNullOrEmpty()) throw new MyTFSConnectionException("Trying to get a local directory before it has been set");
            return paths.Local;
        }

        public void PrintBranchPaths()
        {
            Output.WriteLine("Branches set to:");
            var keys = _branchMap.Keys;
            foreach (BranchType key in keys)
            {
                if (key != BranchType.All)
                {
                    Output.WriteLine("  {0}:", key.ToString());

                    var paths = _branchMap[key];

                    Output.WriteLine("    Local: " + paths.Local + " <==> Server: " + paths.Server);
                }
            }
            Output.WriteLine();
        }

        public void UpdateBranch(BranchType Branch)
        {
            if (!_branchMap.ContainsKey(Branch)) throw new MyTFSConnectionException("Invalid branch: " + Branch.ToString());

            List<BranchType> branches = BranchToList(Branch);
            foreach (var branch in branches)
            {
                try
                {
                    String localPath = _branchMap[branch].Local;
                    var itemSpec = new ItemSpec(localPath, RecursionType.Full);
                    var getRequest = new GetRequest(itemSpec, VersionSpec.Latest);
                    Output.WriteLine("\nUpdating {0} branch...", branch.ToString());
                    //GetStatus getStatus = _workSpace.Get(getRequest, GetOptions.NoAutoResolve);
                    GetStatus getStatus = WorkSpace.Get(getRequest, GetOptions.None);

                    //Output.WriteLine("  {0} files ({1} bytes) downloaded", getStatus.NumFiles, getStatus.NumBytes);
                    Output.WriteLine();
                }
                catch (Exception ex)
                {
                    throw new MyTFSConnectionException("Exception on Get():\n" + ex.ToString());
                }
            }
        }

        public IList<Conflict> UpdateBranch(string localPath, GetOptions getOptions = GetOptions.None)
        {
            if (Directory.Exists(localPath) == false && File.Exists(localPath) == false)
                throw new ArgumentException("Local path does not exist: " + localPath);

            var itemSpec = new ItemSpec(localPath, RecursionType.Full);
            var getRequest = new GetRequest(itemSpec, VersionSpec.Latest);
            GetStatus getStatus = WorkSpace.Get(getRequest, getOptions);

            if (getStatus.NumFailures > 0)
                throw new MyTFSConnectionException(getStatus.NumFailures + " failures when Getting " + localPath);

            return GetConflicts(localPath, getStatus);
        }

        private IList<Conflict> GetConflicts(string localOrServerPath, GetStatus getStatus)
        {
            List<Conflict> conflicts;
            if (getStatus.NumConflicts > 0)
                conflicts = WorkSpace.QueryConflicts(new string[] { localOrServerPath }, true).ToList();
            else
                conflicts = new List<Conflict>();
            return conflicts;
        }

        private void PrintGetStatus(GetStatus status)
        {
            if (status.NumOperations != 0) Output.WriteLine("  {0} get operations performed", status.NumOperations);
            if (status.NumUpdated != 0) Output.WriteLine("  {0} updates performed", status.NumUpdated);
            if (status.NumWarnings != 0) Output.WriteLine("  {0} warnings", status.NumWarnings);
            if (status.NumConflicts != 0) Output.WriteLine("  {0} conflicts", status.NumConflicts);
            if (status.NumFailures != 0) Output.WriteLine("  {0} failures", status.NumFailures);
        }

        private void CheckPathsDefined()
        {
            if (String.IsNullOrEmpty(_branchMap[BranchType.Source].Local)
                && String.IsNullOrEmpty(_branchMap[BranchType.Target].Local))
            {
                throw new MyTFSConnectionException("Need to set Source and target branches before performing operations on them");
            }
        }

        public IList<Conflict> Merge(int changesetFrom, int changesetTo, MergeOptionsEx mergeOptions = MergeOptionsEx.None)
        {
            CheckPathsDefined();

            string sourcePath = _branchMap[BranchType.Source].Local;
            string targetPath = _branchMap[BranchType.Target].Local;

            ChangesetVersionSpec versionFrom = new ChangesetVersionSpec(changesetFrom);
            ChangesetVersionSpec versionTo = new ChangesetVersionSpec(changesetTo);

            GetStatus status = WorkSpace.Merge(sourcePath, targetPath, versionFrom, versionTo, LockLevel.Unchanged, RecursionType.Full, mergeOptions);
            if (status.NoActionNeeded && status.NumOperations == 0)
                Popups.ShowMessage("No changes found when merging cs " + versionFrom.ToString() + "-" + versionTo.ToString() + ".", MessageBoxImage.Asterisk);

            /* Interpreting the return value:
             * http://teamfoundation.blogspot.fi/2006/11/merging-and-resolving-conflicts-using.html
             * NoActionNeeded == true && NumOperations == 0  – means that no changes in source needed to be merged, so no actual changes were pended
             * NoActionNeeded == false && NumOperations > 0 && HaveResolvableWarnings == false  – means that merges were performed, 
             *                   but all conflicts were resolved automatically. Need to check in pended merge changes and that’s about it
             * NoActionNeeded == false && NumConflicts > 0  – merge was performed and there are conflicts to resolve
             * */

            return GetConflicts(targetPath, status);
        }

        public int Checkin(String comment, WorkItem[] workItems)
        {
            CheckPathsDefined();

            var wiCheckinInfos = new List<WorkItemCheckinInfo>();
            foreach (var workItem in workItems)
                wiCheckinInfos.Add(new WorkItemCheckinInfo(workItem, WorkItemCheckinAction.Associate));

            PendingChange[] pendingChanges = GetPendingChanges();

            Output.WriteLine("Checking in {0} pending changes...", pendingChanges.Count());

            if (pendingChanges.Count() == 0)
                throw new MyTFSConnectionException("There were no changes to merge (trying to check in zero pending changes)");

            int checkinId = WorkSpace.CheckIn(pendingChanges, comment, null, wiCheckinInfos.ToArray(), new PolicyOverrideInfo(String.Empty, null));

            Output.WriteLine($"Checkin complete; checkin ID {checkinId}\n");
            return checkinId;
        }

        public PendingChange[] GetPendingChanges()
        {
            return WorkSpace.GetPendingChanges(_branchMap[BranchType.Target].Local, RecursionType.Full);
        }

        public void PrintPendingChanges()
        {
            PendingChange[] pendingChanges = GetPendingChanges();

            Output.WriteLine($"{pendingChanges.Count()} pending changes:");
            foreach (var pendingChange in pendingChanges)
            {
                Output.WriteLine(" " + pendingChange.LocalItem);
            }
            Output.WriteLine();
        }

        public IEnumerable<Changeset> GetHistory(string localPath)
        {
            if (!Directory.Exists(localPath))
                throw new ArgumentException("Local path does not exist: " + localPath);

            try
            {
                var serverPath = WorkSpace.GetServerItemForLocalItem(localPath);
                ItemSpec itemSpec = new ItemSpec(serverPath, RecursionType.Full);
                var changesets = _versionControlServer.QueryHistory(itemSpec);
                return changesets;
            }
            catch (Exception ex)
            {
                throw new MyTFSConnectionException("Exception when getting history :\n" + ex.ToString());
            }
        }

        public IEnumerable<MergeCandidate> GetMergeCandidates()
        {
            CheckPathsDefined();

            // using server paths
            string sourcePath = _branchMap[BranchType.Source].Server;
            string targetPath = _branchMap[BranchType.Target].Server;
            Output.WriteLine("Getting merge candidates\n  from: {0}\n  to: {1}\n  ...", sourcePath, targetPath);

            MergeOptionsEx options = MergeOptionsEx.None;
            ItemSpec sourceItem = new ItemSpec(sourcePath, RecursionType.Full);
            try
            {
                var mergeCandidates = _versionControlServer.GetMergeCandidates(sourceItem, targetPath, options).ToList();
                Output.WriteLine("  merge candidates received.");
                //Output.WriteLine("  {0} merge candidates found.", mergeCandidates.Count());
                //if (PrintCandidates) PrintMergeCandidateList(mergeCandidates);
                return mergeCandidates;
            }
            catch (Exception ex)
            {
                throw new MyTFSConnectionException("Exception when getting merge candidates:\n" + ex.ToString());
            }
        }

        public void PrintMergeCandidateList(List<MergeCandidate> candidates)
        {
            if (candidates.Count > 0)
            {
                int idLength = 7;
                int dateLength = 10;
                int nameLength = 16;
                int commentLength = 50;

                string header1 = "ID".PadRight(idLength);
                string header2 = "DATE".PadRight(dateLength);
                string header3 = "COMMITTER".PadRight(nameLength);
                string header4 = "COMMENT".PadRight(commentLength);

                string header = " " + header1 + " | " + header2 + " | " + header3 + " | " + header4;
                Output.WriteLine(header);

                // Need to print in reverse order, so that the 1st to be merged shows as the lowest on the screen
                for (int ii = candidates.Count() - 1; ii >= 0; ii--)
                {
                    MergeCandidate candidate = candidates[ii];
                    Changeset changeset = candidate.Changeset;

                    //string id = changeset.ChangesetId.ToString().Trim().PadRight(idLength);
                    string idPartialChar = candidate.Partial ? "*" : string.Empty;
                    string id = (idPartialChar + changeset.ChangesetId.ToString("D")).PadLeft(idLength);
                    string date = changeset.CreationDate.ToShortDateString().PadLeft(dateLength);

                    string name = changeset.OwnerDisplayName ?? "";
                    if (name.Length > nameLength) name = name.Substring(0, nameLength);
                    name = name.PadRight(nameLength);

                    string comment = changeset.Comment.Replace("\r\n", "\\n");
                    if (comment.Length > commentLength) comment = comment.Substring(0, commentLength);
                    comment = comment.PadRight(commentLength);

                    string text = " " + id + " | " + date + " | " + name + " | " + comment;
                    Output.WriteLine(text);
                }
            }
        }

        public void PrintChangeset(Changeset changeset)
        {
            Output.WriteLine("  Id: {0}", changeset.ChangesetId);

            Output.WriteLine("  Associated work items ({0}):", changeset.WorkItems.Count());
            foreach (var wi in changeset.WorkItems)
            {
                Output.WriteLine("    " + wi.Id + ": " + wi.Title);
            }
        }

        public WorkItemCollection QueryWorkItemCollection()
        {
            //var teamProject = _projectCollection;
            string teamProjectName = "Revolution40";
            WorkItemCollection workItemCollection = _workItemStore.Query(
                                 " SELECT [System.Id], [System.WorkItemType]," +
                                 " [System.State], [System.AssignedTo], [System.Title] " +
                                 " FROM WorkItems " +
                                 " WHERE [System.TeamProject] = '" + teamProjectName +
                                "' ORDER BY [System.WorkItemType], [System.Id]");
            return workItemCollection;
        }

        /// <summary>
        /// Track a changeset merged into a possible list of branches.
        /// </summary>
        /// <param name="changesetId"></param>
        /// <param name="projectPath"></param>
        /// <param name="branches"></param>
        /// <returns></returns>
        /// <example>
        /// <code>
        /// var mergeBranch = TrackChangesetIn(id, "$/project/dev", new List { "B1", "B2" });
        /// if (mergeBranch.Any())
        /// {
        ///     var targetItems = mergeBranch.Select(mb => mb.TargetItem.Item);
        /// }
        /// </code>
        /// </example>
        public ExtendedMerge[] TrackChangesetIn(int changesetId, string projectPath, IEnumerable<string> branches)
        {
            if (_projectCollection.HasAuthenticated == false)
                _projectCollection.Authenticate();

            var merges = _versionControlServer.TrackMerges(new int[] { changesetId },
                              new ItemIdentifier(projectPath),
                              branches.Select(b => new ItemIdentifier(b)).ToArray(), null);

            return merges;
        }
    }
}

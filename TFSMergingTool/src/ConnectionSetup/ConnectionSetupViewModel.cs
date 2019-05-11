using Caliburn.Micro;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TFSMergingTool.Resources;
using TFSMergingTool.OutputWindow;
using TFSMergingTool.Settings;
using TFSMergingTool.Shell;
using TFSMergingTool.Resources.FolderTree;
using System.IO;
using System.Diagnostics;
using System.Windows;

namespace TFSMergingTool.ConnectionSetup
{
    public interface IServerSetupViewModel : IScreen { }

    [Export(typeof(IServerSetupViewModel))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class ConnectionSetupViewModel : Screen, IServerSetupViewModel
    {
        IEventAggregator EventAggregator { get; set; }
        IOutputWindow Output { get; set; }
        UserSettings UserSettings { get; set; }
        MyTfsConnection TfsConnection { get; set; }
        bool FirstActivationDone { get; set; }
        IPopupService Popups { get; set; }

        [ImportingConstructor]
        public ConnectionSetupViewModel(IEventAggregator eventAggregator, IOutputWindow output,
            UserSettings userSettings, MyTfsConnection tfsConnection, IPopupService popups)
        {
            EventAggregator = eventAggregator;
            Output = output;
            UserSettings = userSettings;
            TfsConnection = tfsConnection;
            Popups = popups;

            Branches = new BindableCollection<BranchViewModel>();

            FirstActivationDone = false;
            Activated += ConnectionSetupViewModel_Activated;
            Deactivated += ConnectionSetupViewModel_Deactivated;
        }

        #region Screen Implementation

        private void ConnectionSetupViewModel_Activated(object sender, ActivationEventArgs e)
        {
            if (!FirstActivationDone)
            {
                FirstActivationDone = true;
                string settingsFileName = UserSettings.DefaultLocalSettingsFileName;
                if (!System.IO.File.Exists(settingsFileName))
                {
                    Output.WriteLine("Default local settings file not found at \"{0}\", loading default settings", settingsFileName);
                    settingsFileName = UserSettings.DefaultSettingsFileName;
                }
                if (!System.IO.File.Exists(settingsFileName))
                {
                    throw new InvalidSettingsFileException("No default settings file found at application startup.");
                }
                LoadSettingsFile(settingsFileName);
            }
            EvalButtonCanProperties();

            if (TfsConnection.IsConnected)
                DisconnectFromServer();
        }

        private void ConnectionSetupViewModel_Deactivated(object sender, DeactivationEventArgs e)
        {
        }

        #endregion

        #region Settings file handling

        public void SelectNewSettingsFile()
        {
            var fileDialog = new OpenFileDialog
            {
                Filter = "Settings files (settings.*.xml)|settings.*.xml",
                InitialDirectory = System.IO.Directory.GetCurrentDirectory()
            };
            if (fileDialog.ShowDialog() == true)
            {
                LoadSettingsFile(fileDialog.FileName);
            }
        }

        private void LoadSettingsFile(string filename)
        {
            Output.WriteLine("Loading settings file {0}...", filename);

            var errorMessage = string.Empty;
            try
            {
                UserSettings.ReadFromFile(filename);
            }
            catch (InvalidSettingsFileException ex)
            {
                errorMessage = ex.Message;
            }

            if (!string.IsNullOrEmpty(errorMessage) || !UserSettings.IsValid)
            {
                var msg = $"Failed to load settings file {filename}";
                if (!string.IsNullOrEmpty(errorMessage))
                    msg += Environment.NewLine + errorMessage;
                msg += Environment.NewLine + "Please fix the settings file and reload it with the button.";

                Output.WriteLine(msg);
                Popups.ShowMessage(null, msg, MessageBoxImage.Asterisk, "Load failed");

                return;
            }

            ServerAddress = UserSettings.ServerUri.ToString();
            TfsExePath = UserSettings.TfsExecutable.FullName;

            Branches.Clear();
            foreach (var branch in UserSettings.BranchPathList)
            {
                var branchWm = new BranchViewModel(branch.Item2, branch.Item1);
                Branches.Add(branchWm);
            }

            Output.WriteLine("Reading settings finished.");
        }

        string _tfsExePath;
        public string TfsExePath
        {
            get => _tfsExePath;
            set
            {
                _tfsExePath = value;
                NotifyOfPropertyChange(() => TfsExePath);
                var fi = new FileInfo(_tfsExePath);
                if (fi.Exists)
                {
                    UserSettings.TfsExecutable = fi;
                }
            }
        }

        public void TestTfsExecutable()
        {
            var branch = SelectedBranche;

            if (branch == null)
            {
                Popups.ShowMessage("Please select a branch; it will be used as the working directory from where to launch the tool.", MessageBoxImage.Asterisk);
                return;
            }

            if (!File.Exists(TfsExePath))
            {
                Popups.ShowMessage($"TFS executable file {TfsExePath} does not exist", MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(branch.Path))
            {
                Popups.ShowMessage($"Working directory {branch.Path} does not exist", MessageBoxImage.Error);
                return;
            }

            // This command will return immediately, so need to run it in a new shell for the user to be able to see the output.
            // https://docs.microsoft.com/en-us/azure/devops/repos/tfvc/status-command

            var p = new Process();
            var psi = new ProcessStartInfo
            {
                WorkingDirectory = branch.Path,
                FileName = "CMD.EXE", 
                Arguments = $"/K \"{TfsExePath}\" status",
                UseShellExecute = false,
            };
            p.StartInfo = psi;
            p.Start();
            p.WaitForExit();
        }

        #endregion

        #region Connect to server

        string _serverAddress;
        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                _serverAddress = value;
                NotifyOfPropertyChange(() => ServerAddress);
                UserSettings.ServerUri = new Uri(ServerAddress);
            }
        }

        public bool ConnectToServer()
        {
            var found = false;

            ConnectionState connectionState = TfsConnection.ConnectToServer(UserSettings.ServerUri,
                out List<TeamProjectCollectionData> teamProjectCollections);

            if (connectionState == ConnectionState.Connected)
            {
                var numCollections = teamProjectCollections.Count;
                Output.WriteLine("Connected to server with {0} collections:", numCollections);
                foreach (var collection in teamProjectCollections)
                    Output.WriteLine("  " + collection.Name);

                // Automatically find a collection and a workspace that contains all the enabled branchpaths.
                var branchPaths = Branches.Where(item => item.IsEnabled == true).Select(item => item.Path).ToList();
                if (branchPaths.Count == 0)
                {
                    Output.WriteLine("No branch paths selected, aborting.");
                    Popups.ShowMessage("Please add some branch paths before connecting to server.", MessageBoxImage.Exclamation);
                    return false;
                }

                foreach (var collection in teamProjectCollections)
                {
                    Output.WriteLine("Connecting to a Team Project Collection {0} at {1}...", collection.Name, collection.Uri);
                    connectionState = TfsConnection.ConnectToTeamProjectCollection(collection.Uri);
                    if (connectionState == ConnectionState.Connected)
                    {
                        Output.WriteLine("Connected.");
                        Workspace[] workSpaces = TfsConnection.GetWorkspaces();
                        foreach (var workspace in workSpaces)
                        {
                            bool hasFolders = DoesWorkspaceContainFolders(workspace, branchPaths);
                            if (hasFolders)
                            {
                                Output.WriteLine("Found all enabled branch folders in workspace " + workspace.Name);
                                TfsConnection.WorkSpace = workspace;
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                        Output.WriteLine("Did not find all enabled branch folders in any one TFS workspace.");
                        Popups.ShowMessage("Did not find all enabled branch folders in any one TFS workspace.", MessageBoxImage.Exclamation);
                    }
                    else
                    {
                        Output.WriteLine("Connection to server failed.");
                        Popups.ShowMessage("Connection to server failed.", MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                Output.WriteLine("Connection to server failed.");
                Popups.ShowMessage("Connection to server failed.", MessageBoxImage.Error);
            }
            
            if (!found)
                DisconnectFromServer();

            EvalButtonCanProperties();

            return found;
        }

        private bool DoesWorkspaceContainFolders(Workspace workspace, List<string> folders)
        {
            foreach (var folder in folders)
            {
                var dInfo = new DirectoryInfo(folder);
                if (!dInfo.Exists)
                    return false;
            }

            // Naive check; even if the workspace contains the parent folder, that folder might not be mapped.

            var foundMap = new Dictionary<string, bool>();
            foreach (var localFolder in folders)
            {
                var localDi = new DirectoryInfo(localFolder);

                var parentFound = false;
                foreach (var wpFolder in workspace.Folders)
                {
                    var wpDi = new DirectoryInfo(wpFolder.LocalItem);
                    parentFound = CheckIfIsSameOrParentOf(localDi, wpDi);
                    if (parentFound)
                    {
                        foundMap.Add(localDi.FullName, true);
                        break;
                    }
                }
                if (!parentFound)
                    foundMap.Add(localDi.FullName, false);
            }

            bool allFound = foundMap.All(item => item.Value == true);
            if (allFound)
                Debug.Assert(foundMap.Keys.Count() == folders.Count);

            return allFound;
        }

        private static bool CheckIfIsSameOrParentOf(DirectoryInfo item, DirectoryInfo parentCandidate)
        {
            bool ret = Utility.PathHelper.PathsEqual(item.FullName, parentCandidate.FullName);
            while (item.Parent != null && ret == false)
            {
                if (Utility.PathHelper.PathsEqual(item.Parent.FullName, parentCandidate.FullName))
                {
                    ret = true;
                    break;
                }
                else item = item.Parent;
            }
            return ret;
        }

        public void DisconnectFromServer()
        {
            Output.WriteLine("Disconnecting from server, if connected.");
            TfsConnection.Disconnect();
            EvalButtonCanProperties();
        }

        #endregion

        #region Button Can properties

        public bool CanGotoMergeFromList => true;

        public bool CanGotoMergeSpecificId => CanGotoMergeFromList;

        public bool CanConnectToServer => !TfsConnection.IsConnected;

        public bool CanDisconnectFromServer => TfsConnection.IsConnected;

        private void EvalButtonCanProperties()
        {
            NotifyOfPropertyChange(() => CanConnectToServer);
            NotifyOfPropertyChange(() => CanDisconnectFromServer);
            NotifyOfPropertyChange(() => CanGotoMergeFromList);
            NotifyOfPropertyChange(() => CanGotoMergeSpecificId);
        }

        #endregion

        #region Branchlist

        public IObservableCollection<BranchViewModel> Branches { get; set; }
        private BranchViewModel _selectedBranch;
        public BranchViewModel SelectedBranche // Caliburn naming convention is "Branches" without the last 's'
        {
            get => _selectedBranch;
            set
            {
                if (value == _selectedBranch) return;
                _selectedBranch = value;
                NotifyOfPropertyChange(() => SelectedBranche);
            }
        }

        public void AddBranch()
        {
            var item = new BranchViewModel("Write the local path here", true);
            Branches.Add(item);
            SelectedBranche = item;
        }

        public void RemoveBranch()
        {
            var index = Branches.IndexOf(SelectedBranche);
            if (index < 0) return;

            Branches.RemoveAt(index);
            if (Branches.Count > 0)
            {
                if (index == Branches.Count)
                    SelectedBranche = Branches[index - 1];
                else
                    SelectedBranche = Branches[index];
            }
        }

        public void MoveBranchUp()
        {
            var item = SelectedBranche;
            var index = Branches.IndexOf(item);
            if (index > 0)
            {
                Branches.RemoveAt(index);
                Branches.Insert(index - 1, item);
                SelectedBranche = item;
            }
        }

        public void MoveBranchDown()
        {
            var item = SelectedBranche;
            var index = Branches.IndexOf(item);
            if (index < Branches.Count - 1)
            {
                Branches.RemoveAt(index);
                Branches.Insert(index + 1, item);
                SelectedBranche = item;
            }
        }

        public void RefreshBranches()
        {
            Branches.Clear();
            foreach (var branch in UserSettings.BranchPathList)
            {
                var branchWm = new BranchViewModel(branch.Item2, branch.Item1);
                Branches.Add(branchWm);
            }
        }

        public void SaveSettings()
        {
            if (Branches.Count <= 0) return;

            var pathList = new List<Tuple<bool, string>>();
            foreach (var branch in Branches)
            {
                pathList.Add(Tuple.Create(branch.IsEnabled, branch.Path));
            }
            UserSettings.BranchPathList = pathList;

            UserSettings.TfsExecutable = new FileInfo(TfsExePath);
            
            UserSettings.WriteToFile(UserSettings.DefaultLocalSettingsFileName);
        }
        #endregion

        #region Start merging

        public void GotoMergeFromList(int mode)
        {
            if (UserSettings.TfsExecutable.Exists == false)
            {
                Popups.ShowMessage("tfs.exe does not exist in:\n" + UserSettings.TfsExecutable.FullName, MessageBoxImage.Error);
                return;
            }

            MainMode newMode;
            switch (mode)
            {
                case 1:
                    newMode = MainMode.MergeFromList;
                    break;
                case 2:
                    newMode = MainMode.MergeSpecificId;
                    break;
                default:
                    return;
            }

            BranchViewModel[] activeBranches = Branches.Where(branch => branch.IsEnabled).ToArray();

            if (activeBranches.Length < 2)
            {
                Popups.ShowMessage("Add at least 2 active branches to start merging.", MessageBoxImage.Exclamation);
            }
            else
            {
                // (re)connect to make sure the correct branches are set.
                if (TfsConnection.IsConnected)
                    DisconnectFromServer();
                ConnectToServer();

                if (TfsConnection.IsConnected)
                {
                    var branchList = activeBranches.Select(activeBranch => new DirectoryInfo(activeBranch.Path)).ToList();
                    EventAggregator.PublishOnUIThread(new ChangeMainModeEvent(newMode, branchList));
                }
            }
        }

        #endregion

    }
}

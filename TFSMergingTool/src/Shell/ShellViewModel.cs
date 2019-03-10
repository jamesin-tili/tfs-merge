using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TFSMergingTool.ConnectionSetup;
using TFSMergingTool.Merging;
using TFSMergingTool.OutputWindow;
using TFSMergingTool.Settings;

namespace TFSMergingTool.Shell
{
    public enum MainMode
    {
        ConnectionSetup,
        MergeFromList,
        MergeSpecificId
    }

    public class ChangeMainModeEvent
    {
        public ChangeMainModeEvent(MainMode newMode)
        {
            NewMode = newMode;
            BranchPaths = null;
        }
        public ChangeMainModeEvent(MainMode newMode, List<DirectoryInfo> branchPaths)
        {
            NewMode = newMode;
            BranchPaths = branchPaths;
        }
        public MainMode NewMode { get; private set; }
        public List<DirectoryInfo> BranchPaths { get; private set; }
    }

    public interface IShell : IProgressWindow { }

    [Export(typeof(IShell)), PartCreationPolicy(CreationPolicy.Shared)]
    class ShellViewModel : Conductor<IScreen>.Collection.OneActive, IShell, IActivate, IDeactivate, IHandle<ChangeMainModeEvent>
    {
        private IWindowManager WindowManager { get; set; }
        private IOutputWindow Output { get; set; }
        private IEventAggregator EventAggregator { get; set; }
        private IServerSetupViewModel ServerSetupVM { get; set; }
        private IMergeFromListViewModel MergeFromListVM { get; set; }
        private IMergeSpecificIdViewModel MergeSpecificIdVM { get; set; }
        private UserSettings Settings { get; set; }

        [ImportingConstructor]
        public ShellViewModel(IWindowManager windowManager, IOutputWindow output, IEventAggregator eventAggregator, UserSettings settings,
            IServerSetupViewModel serverSetupVM, IMergeFromListViewModel mergeFromListVM, IMergeSpecificIdViewModel mergeSpecificIdVM)
        {
            DisplayName = "TFS Merging Tool";

            WindowManager = windowManager;
            Output = output;
            EventAggregator = eventAggregator;
            ServerSetupVM = serverSetupVM;
            MergeFromListVM = mergeFromListVM;
            MergeSpecificIdVM = mergeSpecificIdVM;

            Settings = settings;
            Settings.SetDefaultValues();
            Settings.WriteToFile(Settings.DefaultSettingsFileName);

            Activated += ShellViewModel_Activated;
            Deactivated += ShellViewModel_Deactivated;

            EventAggregator.Subscribe(this);

            ProgressIsShown = false;
            ProgressVisibility = Visibility.Hidden;
            ProgressReporter = new Progress<ProgressReportArgs>(SetProgress);
        }

        #region Screen implementation

        private void ShellViewModel_Activated(object sender, ActivationEventArgs e)
        {
            SetMainView(MainMode.ConnectionSetup, null);
        }

        private void ShellViewModel_Deactivated(object sender, DeactivationEventArgs e)
        {
            // Close all screens.
            var conductedItems = Items;
            bool foundItem;
            do
            {
                foundItem = false;
                var activeItem = (from item in conductedItems
                                  where item.IsActive
                                  select item).FirstOrDefault();
                if (activeItem != null)
                {
                    DeactivateItem(activeItem, true);
                    foundItem = false;
                }
            } while (foundItem);

            bool outputShown = Output.IsShown;
            if (outputShown)
                Output.Hide();
        }

        #endregion

        private void SetMainView(MainMode newMode, List<DirectoryInfo> branchPaths)
        {
            switch (newMode)
            {
                case MainMode.ConnectionSetup:
                    ActivateItem(ServerSetupVM);
                    break;
                case MainMode.MergeFromList:
                    MergeFromListVM.BranchList = branchPaths;
                    ActivateItem(MergeFromListVM);
                    break;
                case MainMode.MergeSpecificId:
                    MergeSpecificIdVM.BranchList = branchPaths;
                    ActivateItem(MergeSpecificIdVM);
                    break;
            }
        }

        public void ToggleOutputWindow()
        {
            ProgressVisibility = Visibility.Visible;
            bool isShown = Output.IsShown;
            if (!isShown)
            {
                Output.Show();
            }
            else
            {
                Output.Hide();
            }
        }

        public void Handle(ChangeMainModeEvent message)
        {
            SetMainView(message.NewMode, message.BranchPaths);
        }

        #region IProgressWindowBase implementation

        private readonly string _PROGRESS_BUTTON_TEXT_CANCEL = "Cancel";
        private readonly string _PROGRESS_BUTTON_TEXT_CANCELING = "Canceling...";
        private readonly string _PROGRESS_BUTTON_TEXT_CLOSE = "Ok";

        public bool _progressIsShown = false;
        public bool ProgressIsShown
        {
            get
            {
                return _progressIsShown;
            }
            set
            {
                if (value != _progressIsShown)
                {
                    _progressIsShown = value;
                    ProgressVisibility = _progressIsShown ? Visibility.Visible : Visibility.Hidden;
                    if (_progressIsShown == true)
                    {
                        ProgressStopButtonText = _PROGRESS_BUTTON_TEXT_CANCEL;
                        _busyCursorWasSetWhenCanceling = false;
                    }
                    NotifyOfPropertyChange(() => ProgressIsShown);
                    NotifyOfPropertyChange(() => ProgressVisibility);
                }
            }
        }

        public double _progressMinimum;
        public double ProgressMinimum
        {
            get
            {
                return _progressMinimum;
            }
            set
            {
                if (value != _progressMinimum)
                {
                    _progressMinimum = value;
                    NotifyOfPropertyChange(() => ProgressMinimum);
                }
            }
        }

        public double _progressMaximum;
        public double ProgressMaximum
        {
            get
            {
                return _progressMaximum;
            }
            set
            {
                if (value != _progressMaximum)
                {
                    _progressMaximum = value;
                    NotifyOfPropertyChange(() => ProgressMaximum);
                }
            }
        }

        public double _progressValue;
        public double ProgressValue
        {
            get
            {
                return _progressValue;
            }
            set
            {
                if (value != _progressValue)
                {
                    _progressValue = value;
                    NotifyOfPropertyChange(() => ProgressValue);
                }
            }
        }

        public string _progressTitle;
        public string ProgressTitle
        {
            get
            {
                return _progressTitle;
            }
            set
            {
                if (value != _progressTitle)
                {
                    _progressTitle = value;
                    NotifyOfPropertyChange(() => ProgressTitle);
                }
            }
        }

        public string _progressState;
        public string ProgressState
        {
            get
            {
                return _progressState;
            }
            set
            {
                if (value != _progressState)
                {
                    _progressState = value;
                    NotifyOfPropertyChange(() => ProgressState);
                }
            }
        }

        public CancellationTokenSource _progressCancelTokenSource;
        public CancellationTokenSource ProgressCancelTokenSource
        {
            get
            {
                return _progressCancelTokenSource;
            }
            set
            {
                if (value != _progressCancelTokenSource)
                {
                    _progressCancelTokenSource = value;
                    NotifyOfPropertyChange(() => ProgressCancelTokenSource);
                }
            }
        }

        public void SetProgress(ProgressReportArgs args)
        {
            if (args.IncrementalValue > 0) ProgressValue += args.IncrementalValue;
            if (args.Title != null) ProgressTitle = args.Title;
            if (args.State != null) ProgressState = args.State;
            if (args.NewMaximumValue > 0) ProgressMaximum = args.NewMaximumValue;
        }

        public IProgress<ProgressReportArgs> ProgressReporter { get; private set; }

        public void ProgressCancelOperation()
        {
            if (ProgressStopButtonText == _PROGRESS_BUTTON_TEXT_CANCELING)
            {
                StopIndicatingCancelIsInProgress();
                ProgressStopButtonText = _PROGRESS_BUTTON_TEXT_CLOSE;
                ProgressIsShown = false;
            }
            else if (ProgressStopButtonText == _PROGRESS_BUTTON_TEXT_CLOSE)
            {
                ProgressIsShown = false;
            }
            else
            {
                if (ProgressCancelTokenSource != null)
                {
                    IndicateCancelIsInProgress();
                    ProgressCancelTokenSource.Cancel();
                }
                //SetFinalMessage(null, "Stopped after: " + ProgressState, false);
            }
        }

        private bool _busyCursorWasSetWhenCanceling;
        private void IndicateCancelIsInProgress()
        {
            ProgressStopButtonText = _PROGRESS_BUTTON_TEXT_CANCELING;
            SetMouseBusyCursor(true);
            _busyCursorWasSetWhenCanceling = true;
        }

        private void StopIndicatingCancelIsInProgress()
        {
            if (_busyCursorWasSetWhenCanceling)
            {
                _busyCursorWasSetWhenCanceling = false;
                SetMouseBusyCursor(false);
            }
        }

        private void SetMouseBusyCursor(bool isBusy)
        {
            var newCursor = isBusy ? System.Windows.Input.Cursors.Wait : null;
            Caliburn.Micro.Execute.OnUIThread(() =>
            {
                System.Windows.Input.Mouse.OverrideCursor = newCursor;
            });
        }

        public void SetFinalMessage(string title = null, string state = null, bool setValueToMax = true)
        {
            if (ProgressIsShown)
            {
                if (!string.IsNullOrEmpty(title)) ProgressTitle = title;
                if (!string.IsNullOrEmpty(state)) ProgressState = state;
                ProgressStopButtonText = _PROGRESS_BUTTON_TEXT_CLOSE;
                if (setValueToMax) ProgressValue = ProgressMaximum;
                StopIndicatingCancelIsInProgress();
            }
        }

        public CancellationToken InitAndShowProgress(double value, double min, double max, string title, string state = "")
        {
            ProgressMinimum = min;
            ProgressMaximum = max;
            ProgressValue = value;
            ProgressTitle = title;
            ProgressState = !string.IsNullOrEmpty(state) ? state : string.Empty;
            ProgressIsShown = true;

            ProgressCancelTokenSource?.Dispose();
            ProgressCancelTokenSource = new CancellationTokenSource();
            return ProgressCancelTokenSource.Token;
        }

        #endregion

        #region Progress window properties

        /// <summary>
        /// Set this using the ProgressIsShown property.
        /// </summary>
        public Visibility ProgressVisibility { get; private set; }

        public string _progressStopButtonText;
        public string ProgressStopButtonText
        {
            get
            {
                return _progressStopButtonText;
            }
            set
            {
                if (value != _progressStopButtonText)
                {
                    _progressStopButtonText = value;
                    NotifyOfPropertyChange(() => ProgressStopButtonText);
                }
            }
        }

        #endregion
    }
}

using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace TFSMergingTool.OutputWindow
{
    [Export(typeof(IOutputWindow))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class OutputWindowViewModel : Screen, IOutputWindow
    {
        public ObservableCollection<string> ConsoleOutput { get; private set; }
        public bool IsShown { get { return IsActive; } }
        public string StatusText { get; private set; }

        private IWindowManager _windowManager;

        [ImportingConstructor]
        public OutputWindowViewModel(IWindowManager WindowManager)
        {
            DisplayName = "Output";

            _windowManager = WindowManager;

            ConsoleOutput = new ObservableCollection<string>();
            writeLine("Output window initialized");
            StatusText = string.Empty;
        }

        private void writeLine(string line)
        {
            Caliburn.Micro.Execute.OnUIThread(() =>
            {
                ConsoleOutput.Add(line);
                if (IsShown)
                {
                    StatusText = ConsoleOutput.Count + " lines";
                    NotifyOfPropertyChange(() => ConsoleOutput);
                    NotifyOfPropertyChange(() => StatusText);
                }
            });
        }

        public void Show()
        {
            if (!IsShown)
            {
                dynamic settings = new ExpandoObject();
                settings.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                _windowManager.ShowWindow(this, null, settings);
                NotifyOfPropertyChange(() => ConsoleOutput);
                NotifyOfPropertyChange(() => StatusText);
            }
        }

        public void WriteLine(string formatStr, params object[] arguments)
        {
            if (formatStr != null)
            {
                if (arguments == null || arguments.Count() == 0)
                {
                    writeLine(string.Format(formatStr));
                }
                else
                {
                    writeLine(string.Format(formatStr, arguments));
                }
            }
        }

        public void WriteLine()
        {
            writeLine(Environment.NewLine);
        }

        public void Hide()
        {
            TryClose();
        }
    }
}

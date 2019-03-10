using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TFSMergingTool.Shell
{
    public interface IProgressWindow
    {
        bool ProgressIsShown { get; set; }
        string ProgressTitle { get; set; }
        string ProgressState { get; set; }
        double ProgressValue { get; set; }
        double ProgressMaximum { get; set; }
        double ProgressMinimum { get; set; }
        CancellationTokenSource ProgressCancelTokenSource { get; set; }
        void SetProgress(ProgressReportArgs args);
        void ProgressCancelOperation();
        
        /// <summary>
        /// Initialize and show the progress window.
        /// </summary>
        /// <returns>A newly created CancellationTokenSource that is used by the cancel button.</returns>
        CancellationToken InitAndShowProgress(double value, double min, double max, string title, string state = "");

        /// <summary>
        /// Shows the messages and displays an ok button for the user.
        /// </summary>
        void SetFinalMessage(string title = null, string state = null, bool setValueToMax = true);

        /// <summary>
        /// Use this to report progress from a Task.
        /// </summary>
        IProgress<ProgressReportArgs> ProgressReporter { get; }
    }

    public class ProgressReportArgs
    {
        /// <summary>
        /// Use this to report incremental progress.
        /// </summary>
        public ProgressReportArgs(int incrValue, string title = null, string state = null, int newMaximumValue = -1)
        {
            IncrementalValue = incrValue;
            Title = title;
            State = state;
            NewMaximumValue = newMaximumValue;
        }

        public int IncrementalValue { get; protected set; }
        public string Title { get; protected set; }
        public string State { get; protected set; }
        public int NewMaximumValue { get; protected set; }
    }
}

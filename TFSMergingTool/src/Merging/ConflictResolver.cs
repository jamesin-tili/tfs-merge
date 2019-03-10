using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TFSMergingTool.Resources;

namespace TFSMergingTool.Merging
{
    public static class ConflictResolver
    {
        /// <summary>
        /// Will query for conflicts in the folder, then offer a choice to resolve them with tfs.exe.
        /// </summary>
        /// <remarks>
        /// Will asks the user if he wants to lauch the tool until either there are no more conflicts,
        /// or until the user chooses not to launch the tool anymore.
        /// </remarks>
        public static Tuple<bool, IList<Conflict>>
            ResolveConflictsWithExternalExecutable(FileInfo tfExecutable, MyTFSConnection tfsConnection, string targetLocalPath, IPopupService popupService, string checkinComment = null)
        {
            Debug.Assert(tfExecutable.Exists, "tfs.exe not found.");
            Debug.Assert(!string.IsNullOrEmpty(targetLocalPath), "Local path not found.");

            bool success = true;

            var conflicts = tfsConnection.WorkSpace.QueryConflicts(new string[] { targetLocalPath }, true);
            while (conflicts.Any())
            {
                string msg = !string.IsNullOrEmpty(checkinComment) ?
                    $"{conflicts.Count()} conflict(s) in target path when merging:\n  \"{checkinComment}\"." :
                    msg = $"{conflicts.Count()} conflict(s) in target path.";

                MessageBoxResult mbResult = popupService.AskYesNoQuestion(msg, "Conflict", "Launch VS merge tool", "Stop the process.");

                //MessageBoxResult mbResult = Application.Current.Dispatcher.Invoke<MessageBoxResult>(() =>
                //{
                //    System.Windows.Style style = new System.Windows.Style();
                //    style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.YesButtonContentProperty, ));
                //    style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.NoButtonContentProperty, ));
                //    return Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, msg, "Conflict", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes, style);
                //});

                switch (mbResult)
                {
                    case MessageBoxResult.Yes:
                        // https://www.visualstudio.com/en-us/docs/tfvc/resolve-command
                        ProcessStartInfo _processStartInfo = new ProcessStartInfo();
                        _processStartInfo.WorkingDirectory = targetLocalPath;
                        _processStartInfo.FileName = tfExecutable.FullName;
                        _processStartInfo.Arguments = "resolve";
                        _processStartInfo.CreateNoWindow = true;
                        Process myProcess = Process.Start(_processStartInfo);
                        myProcess.WaitForExit();
                        break;

                    default:
                        success = false;
                        break;
                }

                if (success == false)
                    break;

                // Check again.
                conflicts = tfsConnection.WorkSpace.QueryConflicts(new string[] { targetLocalPath }, true);
            }

            List<Conflict> retList;
            if (success == true)
                retList = new List<Conflict>();
            else
                retList = conflicts.ToList();
            return new Tuple<bool, IList<Conflict>>(success, retList);
        }
    }


    /* This seemed to work well with the right version dlls, but then again having those versions in all machines seems tricky.
     * Don't really want to worry about this, so maybe its better to use tfs.exe, since that's always available.

    private static void ResolveConflictsWithAPICall(IList<Conflict> conflicts, MyTFSConnection tfsConnection, string checkinComment)
        {
            var conflictMsg = conflicts.Count.ToString() + " merge conflict(s) encountered when merging:\n  \"" + checkinComment + "\".";

            var cMsgCount = 1;
            foreach (var c in conflicts)
            {
                var cMsgTitle = "Merge Conflict " + cMsgCount.ToString() + " of " + conflicts.Count.ToString() + "";

                var cMsg = c.GetFullMessage() + "\n"
                    + "\nSource version: " + c.YourVersion
                    + "\nTarget version: " + c.TheirVersion;

                MessageBoxResult mbResult = Application.Current.Dispatcher.Invoke<MessageBoxResult>(() =>
                {
                    System.Windows.Style style = new System.Windows.Style();
                    style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.YesButtonContentProperty, "Launch VS merge tool"));
                    style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.NoButtonContentProperty, "Stop the process."));
                    return Xceed.Wpf.Toolkit.MessageBox.Show(cMsg, cMsgTitle, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes, style);
                });

                Resolution resolution;
                switch (mbResult)
                {
                    case MessageBoxResult.Yes:
                        /// * This will open the VS visual merge tool.
                         * If it doesn't open, remove the Nuget TeamFoundation packages, and just add every Microsoft.TeamFoundation.x dll under:
                         * C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer * /
                        bool resolved = tfsConnection.WorkSpace.MergeContent(c, true);
                        if (resolved)
                        {
                            resolution = Resolution.AcceptMerge;
                        }
                        else
                        {
                            throw new MyTFSConflictExeption(conflictMsg);
                        }
                        break;

                    default:
                        throw new MyTFSConflictExeption(conflictMsg);
                        break;
                }

                c.Resolution = resolution;
                tfsConnection.WorkSpace.ResolveConflict(c);
                Console.WriteLine("Conflict resolved: " + resolution.ToString());
                cMsgCount++;
            }
        }
    }
    */

}


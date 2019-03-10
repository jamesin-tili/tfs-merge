using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Threading;
using TFSMergingTool.Merging;

namespace TFSMergingTool.Resources
{
    [Export(typeof(IPopupService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class PopupService : IPopupService
    {
        public void ShowMessage(string message, MessageBoxImage messageBoxImage = MessageBoxImage.None, string title = "")
        {
            Application.Current.Dispatcher.Invoke(() => ShowMessage(Application.Current.MainWindow, message, messageBoxImage, title));
        }

        public void ShowMessage(Window owner, string message, MessageBoxImage messageBoxImage = MessageBoxImage.None, string title = "")
        {
            if (string.IsNullOrEmpty(title))
                title = "Merging Tool";

            if (Application.Current.Dispatcher.CheckAccess())
            {
                Xceed.Wpf.Toolkit.MessageBox.Show(owner, message, title, MessageBoxButton.OK, messageBoxImage);
            }
            else
            {
                // Calling thread blocks.
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new System.Action(() =>
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show(owner, message, title, MessageBoxButton.OK, messageBoxImage);
                }));
            }
        }

        public MessageBoxResult AskYesNoQuestion(string message, string title = "", string YesButtonContent = "Yes", string NoButtonContent = "No", MessageBoxResult defaultResult = MessageBoxResult.OK,
            MessageBoxImage messageBoxImage = MessageBoxImage.Question)
        {
            if (string.IsNullOrEmpty(title))
                title = "Merging Tool";

            MessageBoxResult mbResult = MessageBoxResult.Cancel;


            if (Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Style style = new System.Windows.Style();
                style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.YesButtonContentProperty, YesButtonContent));
                style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.NoButtonContentProperty, NoButtonContent));
                mbResult = Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, messageBoxImage, MessageBoxResult.Yes, style);
            }
            else
            {
                // Calling thread blocks.
                mbResult = Application.Current.Dispatcher.Invoke<MessageBoxResult>(() =>
                {
                    System.Windows.Style style = new System.Windows.Style();
                    style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.YesButtonContentProperty, YesButtonContent));
                    style.Setters.Add(new Setter(Xceed.Wpf.Toolkit.MessageBox.NoButtonContentProperty, NoButtonContent));
                    return Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, messageBoxImage, MessageBoxResult.Yes, style);
                }, DispatcherPriority.Normal);
            }

            return mbResult;
        }

        public string AskStringInput(string question, string defaultValue = "", string title = "")
        {
            if (string.IsNullOrEmpty(title))
                title = "Merging Tool";

            if (Application.Current.Dispatcher.CheckAccess())
            {
                var answer = StringInputWindow.Show(Application.Current.MainWindow, question, title, defaultValue);
                return answer;
            }
            else
            {
                // Calling thread blocks.
                object retval = Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Func<string>(() =>
                {
                    var answer = StringInputWindow.Show(Application.Current.MainWindow, question, title, defaultValue);
                    return answer;
                }));
                return (string)retval;
            }

        }

        public List<string> AskBranchSequence(List<string> initialSequence)
        {
            var windowManager = new WindowManager();
            var editVM = new EditBranchOrderViewModel(initialSequence);
            editVM.DisplayName = "Edit Branch Sequence";
            if (windowManager.ShowDialog(editVM) == true)
            {
                return editVM.Result;
            }
            return initialSequence;
        }
    }
}

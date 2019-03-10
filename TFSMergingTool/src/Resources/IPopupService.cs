using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TFSMergingTool.Resources
{
    public interface IPopupService
    {
        void ShowMessage(string message, MessageBoxImage messageBoxImage = MessageBoxImage.None, string title = "");

        void ShowMessage(Window owner, string message, MessageBoxImage messageBoxImage = MessageBoxImage.None, string title = "");

        MessageBoxResult AskYesNoQuestion(string message, string title = "", string YesButtonContent = "Yes",
            string NoButtonContent = "No", MessageBoxResult defaultResult = MessageBoxResult.OK,
            MessageBoxImage messageBoxImage = MessageBoxImage.Question);

        string AskStringInput(string question, string defaultValue = "", string title = "");

        List<string> AskBranchSequence(List<string> initialSequence);
    }
}

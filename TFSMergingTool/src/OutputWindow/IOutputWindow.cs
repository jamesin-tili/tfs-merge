using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.OutputWindow
{
    public interface IOutputWindow
    {
        bool IsShown { get; }
        void Show();
        void Hide();
        
        void WriteLine(string formatStr, params object[] arguments);
        void WriteLine();
    }
}

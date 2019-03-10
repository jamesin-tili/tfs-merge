using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.ConnectionSetup
{
    [Serializable]
    public class Branch
    {
        public string Path { get; set; }
        public bool IsEnabled { get; set; }
    }
}

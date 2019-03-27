using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.Utility
{
    static class PathHelper
    {
        public static bool PathsEqual(string fullPathLeft, string fullPathRight)
        {
            // This just does a case insensitive comparison of the path 
            // strings. Linux users need not worry, because TFS path 
            // comparison is case insensitive anyway.

            var fileLeft = fullPathLeft.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fileRight = fullPathRight.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(fileLeft, fileRight, StringComparison.OrdinalIgnoreCase);
        }
    }
}

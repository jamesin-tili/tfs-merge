using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TFSMergingTool.OutputWindow;

namespace TFSMergingTool.Settings
{
    public class InvalidSettingsFileException : Exception
    {
        public InvalidSettingsFileException(string message) : base(message)
        {
        }

        public InvalidSettingsFileException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    [Export(typeof(UserSettings))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class UserSettings
    {
        public readonly string DefaultSettingsFileName = "Settings.default.xml";
        public string DefaultLocalSettingsFileName { get; set; }
        public Uri ServerUri { get; set; }
        public Tuple<string, string> ServerUsernamePassword { get; set; }
        public List<Tuple<bool, string>> BranchPathList { get; set; }
        public FileInfo TfsExecutable { get; set; }

        public bool IsValid { get; private set; }

        private enum SettingFields
        {
            Info,
            ServerUri,
            Username,
            Password,
            BranchpathList,
            Branchpath,
            BranchIsEnabled,
            TfsExePath
        }

        public UserSettings()
        {
        }

        public void SetDefaultValues()
        {
            DefaultLocalSettingsFileName = "Settings.local.xml";
            ServerUri = new Uri("about::blank");
            ServerUsernamePassword = Tuple.Create(string.Empty, string.Empty);

            var branchPathList = new List<Tuple<bool, string>>();
            branchPathList.Add(Tuple.Create(true, @"C:\MergingWorkspace\Dev"));
            branchPathList.Add(Tuple.Create(true, @"C:\MergingWorkspace\Release"));
            BranchPathList = branchPathList;

            TfsExecutable = FindTfsExecutableFile();

            IsValid = true;
        }

        private FileInfo FindTfsExecutableFile()
        {
            var tryList = new List<string>()
            {
                @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\tf.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\tf.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\tf.exe"
            };
            int count = tryList.Count;

            FileInfo fi = null;
            for (int ii = 0; ii < count; ii++)
            {
                fi = new FileInfo(tryList[ii]);
                if (fi.Exists)
                {
                    break;
                }
                else if (ii == count - 1)
                {
                    string msg = $"Could not find tf.exe. Tried to look in {count} places:";
                    foreach (var dir in tryList)
                    {
                        msg += "\n  " + dir;
                    }
                    System.Windows.MessageBox.Show(msg);
                }
            }

            return fi;
        }

        public void WriteToFile(string Filepath)
        {
            var branchList = new XElement(SettingFields.BranchpathList.ToString());

            foreach (var branch in BranchPathList)
            {
                var branchIsEnabled = new XElement(SettingFields.BranchIsEnabled.ToString(), branch.Item1);
                var branchPath = new XElement(SettingFields.Branchpath.ToString(), branch.Item2);
                branchList.Add(branchPath);
                branchList.Add(branchIsEnabled);
            }

            var xmltree1 = new XElement("UserSettings",
                //new XElement(SettingFields.Info.ToString(), "This is the default settings file. To use your own settings, create a copy of this file, and name it " + DefaultLocalSettingsFileName),
                new XElement(SettingFields.ServerUri.ToString(), ServerUri.ToString()),
                new XElement(SettingFields.Username.ToString(), ServerUsernamePassword.Item1),
                new XElement(SettingFields.Password.ToString(), ServerUsernamePassword.Item2),
                new XElement(SettingFields.TfsExePath.ToString(), TfsExecutable.FullName),
                branchList);

            xmltree1.Save(Filepath);
        }

        /// <summary>
        /// Read settings from disk.
        /// </summary>
        /// <param name="Filepath"></param>
        /// <returns>Returns true if the settings file was read succesfully. 
        /// Returns false if the settings file was not found. 
        /// Throws exception on error in reading the file.</returns>
        public bool ReadFromFile(string Filepath)
        {
            if (!System.IO.File.Exists(Filepath))
            {
                IsValid = false;
                return false;
            }

            try
            {
                XElement xmlTree = XElement.Load(Filepath);

                string username = xmlTree.Element(SettingFields.Username.ToString()).Value;
                string password = xmlTree.Element(SettingFields.Password.ToString()).Value;

                string tfsExePathStr = xmlTree.Element(SettingFields.TfsExePath.ToString()).Value;
                string serverUri = xmlTree.Element(SettingFields.ServerUri.ToString()).Value;
                ServerUri = new Uri(serverUri);

                TfsExecutable = new FileInfo(tfsExePathStr);
                if (TfsExecutable.Exists == false)
                    throw new InvalidSettingsFileException("Error when reading settings: executable file " + TfsExecutable.FullName + " does not exist.");

                var branchListElement = xmlTree.Element(SettingFields.BranchpathList.ToString());
                var branchPathElements = branchListElement.Elements(SettingFields.Branchpath.ToString()).ToList();
                var branchIsEnabledElements = branchListElement.Elements(SettingFields.BranchIsEnabled.ToString()).ToList();
                var branchPathList = new List<Tuple<bool, string>>();
                Debug.Assert(branchPathElements.Count() == branchIsEnabledElements.Count());

                for (int ii = 0; ii < branchPathElements.Count(); ii++)
                {
                    string branchPath = branchPathElements[ii].Value;
                    bool branchIsEnabled = Convert.ToBoolean(branchIsEnabledElements[ii].Value);
                    //if (false && !System.IO.Directory.Exists(branchPath))
                    //{
                    //    IsValid = false;
                    //    throw new InvalidSettingsFileException("Error when reading branch paths from settings: directory " + branchPath + " does not exist.");
                    //}
                    //else
                    //{
                        branchPathList.Add(Tuple.Create(branchIsEnabled, branchPath));
                    //}
                }

                IsValid = true;

                ServerUsernamePassword = Tuple.Create(username, password);
                BranchPathList.Clear();
                foreach (var path in branchPathList)
                {
                    BranchPathList.Add(path);
                }
            }
            catch (Exception)
            {
                IsValid = false;
                return false;
            }
            return IsValid;
        }

    }
}

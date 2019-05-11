using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

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
    internal class UserSettings
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
            // todo: add VS2019 path
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
                if (fi.Exists) break;

                if (ii == count - 1)
                {
                    string msg = $"Could not find tf.exe. Tried to look in {count} places:";
                    foreach (var dir in tryList)
                        msg += "\n  " + dir;

                    System.Windows.MessageBox.Show(msg);
                }
            }

            return fi;
        }

        public void WriteToFile(string filepath)
        {
            var branchList = new XElement(SettingFields.BranchpathList.ToString());

            foreach (var branch in BranchPathList)
            {
                var branchIsEnabled = new XElement(SettingFields.BranchIsEnabled.ToString(), branch.Item1);
                var branchPath = new XElement(SettingFields.Branchpath.ToString(), branch.Item2);
                branchList.Add(branchPath);
                branchList.Add(branchIsEnabled);
            }

            var xmlTree1 = new XElement("UserSettings",
                //new XElement(SettingFields.Info.ToString(), "This is the default settings file. To use your own settings, create a copy of this file, and name it " + DefaultLocalSettingsFileName),
                new XElement(SettingFields.ServerUri.ToString(), ServerUri.ToString()),
                new XElement(SettingFields.Username.ToString(), ServerUsernamePassword.Item1),
                new XElement(SettingFields.Password.ToString(), ServerUsernamePassword.Item2),
                new XElement(SettingFields.TfsExePath.ToString(), TfsExecutable.FullName),
                branchList);

            xmlTree1.Save(filepath);
        }

        /// <summary>
        /// Read settings from disk.
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns>Returns true if the settings file was read succesfully. 
        /// Returns false if the settings file was not found. 
        /// </returns>
        /// <exception cref="InvalidSettingsFileException">Something was wrong with the format.</exception>
        public void ReadFromFile(string filepath)
        {
            if (!System.IO.File.Exists(filepath))
            {
                IsValid = false;
                throw new InvalidSettingsFileException($"Settings file {filepath} does not exist.");
            }

            var xmlTree = XElement.Load(filepath);

            string username = xmlTree.Element(SettingFields.Username.ToString()).Value;
            string password = xmlTree.Element(SettingFields.Password.ToString()).Value;

            string serverUri = xmlTree.Element(SettingFields.ServerUri.ToString()).Value;
            ServerUri = new Uri(serverUri);

            string tfsExePathStr = xmlTree.Element(SettingFields.TfsExePath.ToString()).Value;
            TfsExecutable = new FileInfo(tfsExePathStr);
            if (!TfsExecutable.Exists)
                throw new InvalidSettingsFileException($"TFS command line tool does not exist at {TfsExecutable.FullName}");

            var branchListElement = xmlTree.Element(SettingFields.BranchpathList.ToString());
            var branchPathElements = branchListElement.Elements(SettingFields.Branchpath.ToString()).ToArray();
            var branchIsEnabledElements = branchListElement.Elements(SettingFields.BranchIsEnabled.ToString()).ToArray();
            var branchPathList = new List<Tuple<bool, string>>();
            Debug.Assert(branchPathElements.Count() == branchIsEnabledElements.Count());

            for (int ii = 0; ii < branchPathElements.Count(); ii++)
            {
                string branchPath = branchPathElements[ii].Value;
                bool branchIsEnabled = Convert.ToBoolean(branchIsEnabledElements[ii].Value);
                //if (false && !System.IO.Directory.Exists(branchPath))
                //{
                //    IsValid = false;
                //    throw new InvalidSettingsFileException($"directory {branchPath} does not exist.");
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
                BranchPathList.Add(path);
        }
    }
}

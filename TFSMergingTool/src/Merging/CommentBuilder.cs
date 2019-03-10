using Microsoft.TeamFoundation.VersionControl.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TFSMergingTool.Resources;

namespace TFSMergingTool.Merging
{
    public class CommentBuilder
    {
        /// <summary>
        /// Build a comment for checkin for a single change set. Tries to remove any previously generated comments from the given comment string.
        /// </summary>
        public static string GetComment(string originalComment, int changesetId, string owner, string sourceBranch, string targetBranch, MergeOptionsEx mergeOptions = MergeOptionsEx.None)
        {
            string retval;

            string originalOwner;
            string commentPart = TryToRemoveOldPrefix(originalComment, out originalOwner);

            string ownerShort = !string.IsNullOrEmpty(originalOwner) ?
                ShortenOwnerName(originalOwner) :
                ShortenOwnerName(owner);

            string optionsPrefix = GetOptionsString(mergeOptions);
            if (!string.IsNullOrEmpty(optionsPrefix)) optionsPrefix = optionsPrefix + ", ";

            retval = string.Format(optionsPrefix + "{0} {1} > {2}, {3}: {4}", sourceBranch, changesetId, targetBranch, ownerShort, commentPart);

            return retval;
        }

        /// <summary>
        /// Build a comment for checkin for a range of change sets.
        /// </summary>
        public static string GetCombinedMergeCheckinComment(string sourceBranch, string targetBranch, IEnumerable<Tuple<int, string>> idAndOwnerOfChanges, MergeOptionsEx mergeOptions)
        {
            int firstChangeset = idAndOwnerOfChanges.Min(ch => ch.Item1);
            int lastChangeset = idAndOwnerOfChanges.Max(ch => ch.Item1);
            var owners = idAndOwnerOfChanges.Select(cs => cs.Item2).Distinct();

            string ownerStr;
            int ownerCount = owners.Count();
            if (ownerCount == 1)
            {
                ownerStr = ShortenOwnerName(owners.First());
            }
            //else if (ownerCount < 3)
            //{
            //    var shortOwners = ShortenOwnerNames(owners.ToList());
            //    ownerStr = string.Join(", ", shortOwners);
            //}
            else
            {
                ownerStr = ownerCount.ToString() + " authors";
            }

            string optionsPrefix = GetOptionsString(mergeOptions);
            if (!string.IsNullOrEmpty(optionsPrefix)) optionsPrefix = optionsPrefix + ", ";

            string branchStr = $"{sourceBranch} {firstChangeset}-{lastChangeset} > {targetBranch}";

            string comment = optionsPrefix + branchStr + ", " + ownerStr + ": ";
            if (mergeOptions.HasFlag(MergeOptionsEx.AlwaysAcceptMine))
            {
                var popups = Caliburn.Micro.IoC.Get<IPopupService>();
                var answer = popups.AskYesNoQuestion("Is this a \"Cleaning history\" case?");
                if (answer == System.Windows.MessageBoxResult.Yes)
                {
                    comment = $"Cleaning merge history ({comment})";
                }
            }

            return comment;
        }

        #region regex constants

        const string RX_BEGIN = @"^";
        const string RX_BRANCH = @"([\w\.\-_]+)";
        const string RX_ID = @"(\d+)";
        const string RX_ID_RANGE = @"(\d+\-\d+)";
        const string RX_OWNER = @"(\w+ \w+)";
        const string RX_OWNER_MANY = @"(\d+ authors)";
        const string RX_OPTION = @"(\w+)";

        const string RX_SINGLE_CHANGESET = RX_BEGIN + RX_BRANCH + " " + RX_ID + " > " + RX_BRANCH + ", " + RX_OWNER + ": ";
        const string RX_RANGE = RX_BEGIN + RX_BRANCH + " " + RX_ID_RANGE + " > " + RX_BRANCH + ", " + RX_OWNER + ": ";
        const string RX_RANGE_MANY = RX_BEGIN + RX_BRANCH + " " + RX_ID_RANGE + " > " + RX_BRANCH + ", " + RX_OWNER_MANY + ": ";

        private static List<string> _prefixPatterns = new List<string>()
        {
            RX_SINGLE_CHANGESET,
            RX_OPTION + " " + RX_SINGLE_CHANGESET,
            RX_RANGE,
            RX_OPTION + " " + RX_RANGE,
            RX_RANGE_MANY,
            RX_OPTION + " " + RX_RANGE_MANY
        };

        #endregion

        private static string TryToRemoveOldPrefix(string comment, out string originalOwner)
        {
            var retval = string.Empty;
            originalOwner = string.Empty;

            var regExOptions = System.Text.RegularExpressions.RegexOptions.IgnoreCase;

            var oldPrefixFound = false;
            var currentPattern = 0;
            foreach (var pattern in _prefixPatterns)
            {
                string[] match = System.Text.RegularExpressions.Regex.Split(comment, pattern, regExOptions);
                if (match.Length > 1 && String.IsNullOrEmpty(match[0]))
                {
                    // Select the original comment.
                    int matches = match.Count();
                    if (matches > 1)
                    {
                        originalOwner = match[matches - 2];
                    }
                    retval = match.Last();
                    oldPrefixFound = true;
                    break;
                }
                currentPattern++;
            }
            if (oldPrefixFound == false) retval = comment;

            return retval;
        }

        private static string ShortenOwnerName(string original)
        {
            string retval;
            var ownerFirstAndLastName = original.Split(' ');
            if (ownerFirstAndLastName.Count() > 1 && ownerFirstAndLastName.Last().Length > 0)
            {
                bool firstIsNumeric = int.TryParse(ownerFirstAndLastName.First(), out int n);
                if (firstIsNumeric && ownerFirstAndLastName.Last() == "authors")
                {
                    retval = original;
                }
                else
                {
                    // Shorten the last name.
                    retval = ownerFirstAndLastName.First() + " " + ownerFirstAndLastName.Last().ElementAt(0);
                }
            }
            else
            {
                retval = original;
            }
            return retval;
        }

        private static IList<string> ShortenOwnerNames(IList<string> originals)
        {
            var retval = new List<string>(originals.Count);
            for (int ii = 0; ii < originals.Count; ii++)
            {
                retval[ii] = ShortenOwnerName(originals[ii]);
            }
            return retval;
        }

        /// <summary>
        /// Converts MergeOptionsEx to string. Returns an empty string if options is None.
        /// </summary>
        /// <remarks>Does some replace work to improve readability, e.g. AlwaysAcceptMine -> Discard.</remarks>
        private static string GetOptionsString(MergeOptionsEx options)
        {
            string retval;

            if (options == MergeOptionsEx.None)
            {
                retval = string.Empty;
            }
            else
            {
                retval = options.ToString();

                if (options.HasFlag(MergeOptionsEx.ForceMerge))
                {
                    retval = retval.Replace("ForceMerge", "Force");
                }
                else if (options.HasFlag(MergeOptionsEx.AlwaysAcceptMine))
                {
                    retval = retval.Replace("AlwaysAcceptMine", "Discard");
                }
            }

            return retval;
        }
    }
}

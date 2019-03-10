using Microsoft.VisualStudio.TestTools.UnitTesting;
using TFSMergingTool.Merging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.TeamFoundation.VersionControl.Common;

namespace TFSMergingTool.Tests
{
    [TestClass()]
    public class CommentBuilderTests
    {
        const string _source = "Source";
        const string _target = "Target";
        const int _changesetId = 1001;
        const string _ownerLong = "Jörgen Poutanen";
        const string _ownerShort = "Jörgen P";
        const string _someComment = "This is great!";

        [TestMethod()]
        public void GetComment_Basic()
        {
            // "{0} {1} > {2}, {3}: {4}"
            string comment = CommentBuilder.GetComment(_someComment, _changesetId, _ownerLong, _source, _target, MergeOptionsEx.None);
            string expected = $"{_source} {_changesetId} > {_target}, {_ownerShort}: {_someComment}";
            PrintResults(null, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_ReplacePreviousPrefix()
        {
            string commentWithOldPrefix = $"{_source} {_changesetId} > {_target}, {_ownerShort}: {_someComment}";

            const string newSource = "NewSource";
            const string newTarget = "NewTarget";
            const int newId = _changesetId + 1;
            const string mergeOwner = "IShould NotOwnThis";

            string comment = CommentBuilder.GetComment(commentWithOldPrefix, newId, mergeOwner, newSource, newTarget, MergeOptionsEx.None);
            string expected = $"{newSource} {newId} > {newTarget}, {_ownerShort}: {_someComment}";
            PrintResults(commentWithOldPrefix, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_ReplacePreviousPrefix_NonCaseSensitive()
        {
            string commentWithOldPrefix = $"{_source.ToLowerInvariant()} {_changesetId} > {_target.ToLowerInvariant()}, {_ownerShort.ToLowerInvariant()}: {_someComment}";

            const string newSource = "NewSource";
            const string newTarget = "NewTarget";
            const int newId = _changesetId + 1;
            const string mergeOwner = "IShould NotOwnThis";

            string comment = CommentBuilder.GetComment(commentWithOldPrefix, newId, mergeOwner, newSource, newTarget, MergeOptionsEx.None);
            string expected = $"{newSource} {newId} > {newTarget}, {_ownerShort.ToLowerInvariant()}: {_someComment}";
            PrintResults(commentWithOldPrefix, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_ReplacePreviousPrefix_WithSpecialChars()
        {
            const string oldSource = "Old.Sour-ce";
            const string oldTarget = "Old.Tar_get";
            string commentWithOldPrefix = $"{oldSource} {_changesetId} > {oldTarget}, {_ownerShort}: {_someComment}";

            const string newSource = "New.Sour_ce";
            const string newTarget = "New.Tar-get";
            const int newId = _changesetId + 1;
            const string mergeOwner = "IShould NotOwnThis";

            string comment = CommentBuilder.GetComment(commentWithOldPrefix, newId, mergeOwner, newSource, newTarget, MergeOptionsEx.None);
            string expected = $"{newSource} {newId} > {newTarget}, {_ownerShort}: {_someComment}";
            PrintResults(commentWithOldPrefix, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_ReplacePreviousPrefix_WasRangeWithOneOwner()
        {
            string commentWithOldPrefix = $"{_source} {_changesetId}-{_changesetId + 1} > {_target}, {_ownerShort}: {_someComment}";

            const string newSource = "NewSource";
            const string newTarget = "NewTarget";
            const int newId = _changesetId + 10;

            string comment = CommentBuilder.GetComment(commentWithOldPrefix, newId, _ownerLong, newSource, newTarget, MergeOptionsEx.None);
            string expected = $"{newSource} {newId} > {newTarget}, {_ownerShort}: {_someComment}";
            PrintResults(commentWithOldPrefix, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_ReplacePreviousPrefix_WasRangeWithOneOwner_WithSpecialChars()
        {
            const string oldSource = "Old.Sour-ce";
            const string oldTarget = "Old.Tar_get";

            string commentWithOldPrefix = $"{oldSource} {_changesetId}-{_changesetId + 1} > {oldTarget}, {_ownerShort}: {_someComment}";

            const string newSource = "NewSource";
            const string newTarget = "NewTarget";
            const int newId = _changesetId + 10;

            string comment = CommentBuilder.GetComment(commentWithOldPrefix, newId, _ownerLong, newSource, newTarget, MergeOptionsEx.None);
            string expected = $"{newSource} {newId} > {newTarget}, {_ownerShort}: {_someComment}";
            PrintResults(commentWithOldPrefix, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_ReplacePreviousPrefix_WasRangeWithManyOwners()
        {
            const int ownerCount = 3;
            string desiredOwner = $"{ownerCount} authors";
            string commentWithOldPrefix = 
                $"{_source} {_changesetId}-{_changesetId + 4} > {_target}, " + desiredOwner + $": {_someComment}";

            const string newSource = "NewSource";
            const string newTarget = "NewTarget";
            const int newId = _changesetId + 10;

            string comment = CommentBuilder.GetComment(commentWithOldPrefix, newId, _ownerLong, newSource, newTarget, MergeOptionsEx.None);
            string expected = $"{newSource} {newId} > {newTarget}, {desiredOwner}: {_someComment}";
            PrintResults(commentWithOldPrefix, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_ReplacePreviousPrefix_WasRangeWithManyOwners_WithSpecialChars()
        {
            const string oldSource = "Old.Sour-ce";
            const string oldTarget = "Old.Tar_get";

            const int ownerCount = 3;
            string desiredOwner = $"{ownerCount} authors";
            string commentWithOldPrefix =
                $"{oldSource} {_changesetId}-{_changesetId + 4} > {oldTarget}, " + desiredOwner + $": {_someComment}";

            const string newSource = "NewSource";
            const string newTarget = "NewTarget";
            const int newId = _changesetId + 10;

            string comment = CommentBuilder.GetComment(commentWithOldPrefix, newId, _ownerLong, newSource, newTarget, MergeOptionsEx.None);
            string expected = $"{newSource} {newId} > {newTarget}, {desiredOwner}: {_someComment}";
            PrintResults(commentWithOldPrefix, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_MergeRange_Basic_OneOwner()
        {
            var idAndOwnerOfChanges = new List<Tuple<int, string>>();
            idAndOwnerOfChanges.Add(Tuple.Create(_changesetId, _ownerLong));
            idAndOwnerOfChanges.Add(Tuple.Create(_changesetId + 1, _ownerLong));
            idAndOwnerOfChanges.Add(Tuple.Create(_changesetId + 2, _ownerLong));

            string comment = CommentBuilder.GetCombinedMergeCheckinComment(_source, _target, idAndOwnerOfChanges, MergeOptionsEx.None);
            string expected = $"{_source} {_changesetId}-{_changesetId + 2} > {_target}, {_ownerShort}: ";
            PrintResults(null, expected, comment);
            comment.Should().Be(expected);
        }

        [TestMethod()]
        public void GetComment_MergeRange_Basic_MultipleOwners()
        {
            var idAndOwnerOfChanges = new List<Tuple<int, string>>();
            idAndOwnerOfChanges.Add(Tuple.Create(_changesetId, _ownerLong));
            idAndOwnerOfChanges.Add(Tuple.Create(_changesetId + 1, _ownerLong));
            idAndOwnerOfChanges.Add(Tuple.Create(_changesetId + 5, "3 authors"));
            idAndOwnerOfChanges.Add(Tuple.Create(_changesetId + 2, "Another Owner"));

            string comment = CommentBuilder.GetCombinedMergeCheckinComment(_source, _target, idAndOwnerOfChanges, MergeOptionsEx.None);
            string expected = $"{_source} {_changesetId}-{_changesetId + 5} > {_target}, 3 authors: ";
            PrintResults(null, expected, comment);
            comment.Should().Be(expected);
        }

        private void PrintResults(string input, string expected, string result)
        {
            if (!string.IsNullOrEmpty(input))
            {
                Console.WriteLine("The input was:");
                Console.WriteLine(input);
                Console.WriteLine();
            }
            Console.WriteLine("Expected vs result:");
            Console.WriteLine(expected);
            Console.WriteLine(result);
        }
    }
}
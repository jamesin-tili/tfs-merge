using Microsoft.TeamFoundation.VersionControl.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.src.Merging.Options
{
    [Flags]
    public enum MyCheckinOptions
    {
        None = 0,
        NoCheckin = 1
    }

    /// <summary>
    /// Holds current options, and converts between booleans and the MergeOptionsEx enum.
    /// </summary>
    public class MyOptions
    {
        public MyOptions()
        {
            SetDefaultOptions();
        }

        public void SetDefaultOptions()
        {
            _discard = false;
            _force = false;
            _baseless = false;
            _useRange = false;
            CheckinOptions = MyCheckinOptions.None;
        }

        public bool Discard
        {
            set { _discard = value; }
            get { return MergeOptions.HasFlag(MergeOptionsEx.AlwaysAcceptMine); }
        }

        public bool Force
        {
            set { _force = value; }
            get { return MergeOptions.HasFlag(MergeOptionsEx.ForceMerge); }
        }

        public bool Baseless
        {
            set { _baseless = value; }
            get { return MergeOptions.HasFlag(MergeOptionsEx.Baseless); }
        }

        public bool UseRange
        {
            set { _useRange = value; }
            get { return _useRange; }
        }

        public MergeOptionsEx MergeOptions
        {
            get
            {
                MergeOptionsEx retval = MergeOptionsEx.None;
                if (_discard) AddToMergeOptions(ref retval, MergeOptionsEx.AlwaysAcceptMine);
                if (_force) AddToMergeOptions(ref retval, MergeOptionsEx.ForceMerge);
                if (_baseless) AddToMergeOptions(ref retval, MergeOptionsEx.Baseless);

                return retval;
            }
        }

        public MyCheckinOptions CheckinOptions;

        private bool _discard, _force, _useRange, _baseless;

        /// <summary>
        /// Appends the given flag if there are already options other than None, othserwise replaces None.
        /// </summary>
        private void AddToMergeOptions(ref MergeOptionsEx original, MergeOptionsEx newFlag)
        {
            if (original != MergeOptionsEx.None) original |= newFlag;
            else original = newFlag;
        }
    }
}

using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TFSMergingTool.Resources;

namespace TFSMergingTool.Merging
{
    public class CandidateListItem
    {
        public CandidateListItem(Changeset changeset, bool partial, bool isSelected, bool getWorkItemDetails, MyTfsConnection myTfsConnection)
        {
            this.Changeset = changeset;
            this.Partial = partial;
            this.IsSelected = isSelected;

            if (getWorkItemDetails)
            {
                this._tfs = myTfsConnection;
                GetWorkItemData();
            }
        }

        public bool Partial { get; protected set; }
        public Changeset Changeset { get; protected set; }
        public bool IsSelected { get; set; }

        private readonly MyTfsConnection _tfs;
        private bool _isInitialized = false;

        private void GetWorkItemData()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            var resultSb = new StringBuilder();
            if (Changeset == null)
            {
                resultSb.Append("The Changeset object was null!");
            }
            else
            {
                int wiCount = Changeset.WorkItems.Count();
                if (wiCount == 0)
                {
                    resultSb.Append("-");
                }
                else
                {
                    // Create a string that lists interesting work item data.
                    var addedIds = new Collection<int>();
                    for (int ii = 0; ii < wiCount; ii++)
                    {
                        WorkItem workItem = Changeset.WorkItems[ii];
                        var wiProperties = new WorkItemProperties();
                        bool success = GetWorkItemLinkedDataRecursive(workItem, ref wiProperties, allowRecursion: true);

                        if (!success)
                        {
                            // Failed with recursion -> try without.
                            success = GetWorkItemLinkedDataRecursive(workItem, ref wiProperties, allowRecursion: false);
                        }

                        // Parent items are also searched for Tasks, so we can receive the same item many times here.
                        if (addedIds.Contains(wiProperties.Id)) continue;

                        if (success)
                        {
                            WiProperties = wiProperties;

                            addedIds.Add(wiProperties.Id);
                            resultSb.Append(wiProperties.Id.ToString());
                            if (ii < wiCount - 1) resultSb.Append(", ");
                        }
                        else
                        {
                            WiProperties = new WorkItemProperties();
                        }
                    }
                }
            }
            WorkItemList = resultSb.ToString();
        }

        public class WorkItemProperties
        {
            const int DEFAULT_INT = -1;
            const string DEFAULT_STRING = "-";

            public int Id { get; set; } = DEFAULT_INT;
            public string Type { get; set; } = DEFAULT_STRING;
            public string Title { get; set; } = DEFAULT_STRING;
            public string Origin { get; set; } = DEFAULT_STRING;
            public string State { get; set; } = DEFAULT_STRING;
            public string AssignedTo { get; set; } = DEFAULT_STRING;
            public string Severity { get; set; } = DEFAULT_STRING;
            public int Priority { get; set; } = DEFAULT_INT;
            public string PlannedRelease { get; set; } = DEFAULT_STRING;
            public WorkItem WorkItemObject { get; set; }
        }
        
        public string WorkItemList { get; private set; }

        /// <summary>Details for one work item.</summary>
        public WorkItemProperties WiProperties { get; protected set; }

        private const string FIELD_TYPE = "Work Item Type";
        private const string FIELD_TITLE = "Title";
        private const string FIELD_ORIGIN = "Origin";
        private const string FIELD_ASSIGNED_TO = "Assigned To";
        private const string FIELD_SEVERITY = "Severity";
        private const string FIELD_PRIORITY = "Priority";
        private const string FIELD_PLANNED_RELEASE = "Planned Release";
        private const string LINK_TYPE_PARENT = "Parent";
        private const string LINK_TYPE_AFFECTS = "Affects";
        private const string LINK_TYPE_AFFECTEDBY = "Affected By";
        private const string WI_TYPE_BUG = "Bug";
        private const string WI_TYPE_BACKLOG = "Product Backlog Item";
        private const string WI_TYPE_TASK = "Task";

        private const int MAX_RECURSION_DEPTH = 10;
        private int _currentRecursionDepth;
        private List<int> _recursedWorkItemIds;

        private bool GetWorkItemLinkedDataRecursive(Microsoft.TeamFoundation.WorkItemTracking.Client.WorkItem workItem,
            ref WorkItemProperties wiProperties, bool allowRecursion, bool isRecursiveCall = false)
        {
            // note: This method makes some assumptions about how the different items are connected in TFS.
            // Read more to find out which assumptions ;)

            wiProperties.Id = workItem.Id;
            wiProperties.WorkItemObject = workItem;

            if (!isRecursiveCall)
            {
                _currentRecursionDepth = 0;
                _recursedWorkItemIds = new List<int>();
            }
            else
            {
                if (_recursedWorkItemIds.Contains(workItem.Id))
                {
                    //Caliburn.Micro.IoC.Get<IPopupService>().ShowMessage(
                    //$"Got into a recursion cycle: encountered the same work item again (#{workItem.Id}: {workItem.Description}) Aborting search.");
                    return false;
                }

                if (_currentRecursionDepth >= MAX_RECURSION_DEPTH)
                {
                    //Caliburn.Micro.IoC.Get<IPopupService>().ShowMessage(
                    //$"Max recursion depth of {MAX_RECURSION_DEPTH} reached when reading data for work item #{workItem.Id}: {workItem.Description}. Aborting search.");
                    return false;
                }
            }

            _recursedWorkItemIds.Add(workItem.Id);
            _currentRecursionDepth++;

            bool retval = true;
            if (!workItem.Fields.Contains(FIELD_TYPE)) return retval;

            wiProperties.State = workItem.State;

            // Recursion is only performed for Tasks; we don't care about the parents of Bugs or Backlog items.

            Field itemTypeField = workItem.Fields[FIELD_TYPE];
            wiProperties.Type = ShortenWorkItemType((string)itemTypeField.Value);
            if (allowRecursion && wiProperties.Type == WI_TYPE_TASK)
            {
                // note: Tasks often have just one link to the parent (bug or backlog) item.
                var wiLinks = workItem.WorkItemLinks;
                if (wiLinks.Count == 0)
                {
                    //retStr = $"[{id}: {type}: {state}]";
                }
                else
                {
                    WorkItemLink parent = null;
                    if (wiLinks.Count == 1)
                    {
                        // If there's only one link, take that.
                        parent = wiLinks[0];
                    }
                    else
                    {
                        // Multiple links -> Search for Parent (Assuming only 1 parent).
                        foreach (var wiLinkObject in wiLinks)
                        {
                            var wiLink = wiLinkObject as WorkItemLink;
                            if (wiLink == null) continue;

                            string linkTypeName = wiLink.LinkTypeEnd.Name;
                            if (!IsAcceptedLinkType(linkTypeName)) continue;

                            string wiType = GetWorkItemType(_tfs, wiLink.TargetId);
                            if (!IsAcceptedWorkItemType(wiType)) continue;

                            parent = wiLink;
                            break;
                        }

                        //debug
                        if (parent == null)
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine($"Did not find a suitable parent item for a Task with recursion (depth {_currentRecursionDepth}).");
                            sb.AppendLine("Found these link type names:");
                            foreach (var wiLinkObject in wiLinks)
                            {
                                sb.AppendLine((wiLinkObject as WorkItemLink)?.LinkTypeEnd.Name);
                            }
                            Caliburn.Micro.IoC.Get<IPopupService>()?.ShowMessage(sb.ToString());
                        }
                    }

                    if (parent != null && _tfs != null)
                    {
                        WorkItem linkedItem = _tfs.GetWorkItem(parent.TargetId);
                        retval = GetWorkItemLinkedDataRecursive(linkedItem, ref wiProperties, 
                            allowRecursion: true, isRecursiveCall: true);
                    }
                    else
                    {
                        //retStr = $"[{id}: {type}: {state}]";
                    }
                }
            }
            else
            {
                // Not Task -> We'll take this item, no recursion

                if (workItem.Fields.Contains(FIELD_ORIGIN))
                {
                    var originField = workItem.Fields[FIELD_ORIGIN];
                    wiProperties.Origin = (string)originField.Value;
                }

                if (workItem.Fields.Contains(FIELD_TITLE))
                {
                    var titleField = workItem.Fields[FIELD_TITLE];
                    wiProperties.Title = (string)titleField.Value;
                }

                if (workItem.Fields.Contains(FIELD_ASSIGNED_TO))
                {
                    var assignedToField = workItem.Fields[FIELD_ASSIGNED_TO];
                    wiProperties.AssignedTo = (string)assignedToField.Value;
                }

                if (workItem.Fields.Contains(FIELD_SEVERITY))
                {
                    var severityField = workItem.Fields[FIELD_SEVERITY];
                    wiProperties.Severity = (string)severityField.Value;
                }

                if (workItem.Fields.Contains(FIELD_PRIORITY))
                {
                    var priorityField = workItem.Fields[FIELD_PRIORITY];
                    wiProperties.Priority = (int)priorityField.Value;
                }

                if (workItem.Fields.Contains(FIELD_PLANNED_RELEASE))
                {
                    var releaseField = workItem.Fields[FIELD_PLANNED_RELEASE];
                    wiProperties.PlannedRelease = (string)releaseField.Value;
                }

                //retStr = $"[{id}: {type}: {state}]";

            }

            return retval;
        }

        private static bool IsAcceptedWorkItemType(string workItemType)
        {
            return workItemType == WI_TYPE_BUG || workItemType == WI_TYPE_BACKLOG;
        }

        private static bool IsAcceptedLinkType(string linkTypeName)
        {
            return linkTypeName == LINK_TYPE_PARENT || linkTypeName == LINK_TYPE_AFFECTS;
        }

        private static string ShortenWorkItemType(string originalType)
        {
            switch (originalType)
            {
                case "Product Backlog Item":
                    return "Backlog";
                default:
                    return originalType;
            }
        }

        private static string GetWorkItemType(MyTfsConnection tfs, int id)
        {
            var workItem = tfs.GetWorkItem(id);
            return workItem.Type.Name;
        }
    }
}

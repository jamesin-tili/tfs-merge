using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFSMergingTool.Merging
{
    public static class WorkItemHelper
    {
        /// <summary>
        /// Returns comma-separated string that lists the work item Ids.
        /// </summary>
        public static string WorkItemsToString(WorkItem[] workItems)
        {
            int wiCount = workItems.Count();
            if (wiCount == 0)
                return "-";

            var sb = new StringBuilder();
            int ii = 0;
            foreach (var wi in workItems)
            {
                sb.Append(wi.Id.ToString());
                if (ii < wiCount - 1) sb.Append(", ");
                ii++;
            }
            return sb.ToString();
        }

        public static Field GetFieldIfExists(WorkItem workItem, string fieldName)
        {
            FieldCollection fields = workItem.Fields;
            Field retval = fields.Contains(fieldName) ? fields[fieldName] : null;
            return retval;
        }

        internal static void OpenWorkItemInBrowser(WorkItem workItem)
        {
            string collectionPath = workItem.Project.Store.TeamProjectCollection.DisplayName;
            string project = workItem.AreaPath;
            string command = $@"_workItems#id={workItem.Id}&fullScreen=true&_a=edit";
            string fullAddress = $@"{collectionPath}/{project}/{command}";

            System.Diagnostics.Process.Start(fullAddress);

            //const string workItemUrl = @"{0}://{1}:{2}/tfs/web/wi.aspx?id={3}";
            //string scheme = wi.Store.TeamProjectCollection.Uri.Scheme;
            //string server = wi.Store.TeamProjectCollection.Uri.Host;
            //int port = wi.Store.TeamProjectCollection.Uri.Port;

            //string url = string.Format(workItemUrl, scheme, server, port, wi.Id);
            //System.Diagnostics.Process.Start(url);
        }
    }
}

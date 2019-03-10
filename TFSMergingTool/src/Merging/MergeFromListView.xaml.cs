using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TFSMergingTool.Merging
{
    /// <summary>
    /// Interaction logic for MergingMainView.xaml
    /// </summary>
    public partial class MergeFromListView : UserControl
    {
        public MergeFromListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Prevents automatic scrolling to selected items. Annoying when selecting the comment line.
        /// </summary>
        private void DataGrid_Documents_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        //private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        //{
        //    var scv = (ScrollViewer)sender;
        //    scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta / 2.0);
        //    e.Handled = true;
        //}

        private void CandidateListView_Sorting(object sender, DataGridSortingEventArgs e)
        {
            var dgSender = (DataGrid)sender;
            var cView = CollectionViewSource.GetDefaultView(dgSender.ItemsSource);

            //Alternate between ascending/descending if the same column is clicked 
            ListSortDirection direction = ListSortDirection.Ascending;
            if (cView.SortDescriptions.FirstOrDefault().PropertyName == e.Column.SortMemberPath)
                direction = cView.SortDescriptions.FirstOrDefault().Direction == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            cView.SortDescriptions.Clear();
            AddSortColumn(dgSender, e.Column.SortMemberPath, direction);
            //To this point the default sort functionality is implemented

            //Now check the wanted columns and add multiple sort
            const string idColumn = "Changeset.ChangesetId";
            if (e.Column.SortMemberPath != idColumn)
            {
                AddSortColumn(dgSender, idColumn, ListSortDirection.Descending);
            }
            e.Handled = true;
        }

        private void AddSortColumn(DataGrid sender, string sortColumn, ListSortDirection direction)
        {
            var cView = CollectionViewSource.GetDefaultView(sender.ItemsSource);
            cView.SortDescriptions.Add(new SortDescription(sortColumn, direction));
            //Add the sort arrow on the DataGridColumn
            foreach (var col in sender.Columns.Where(x => x.SortMemberPath == sortColumn))
            {
                col.SortDirection = direction;
            }
        }
    }
}

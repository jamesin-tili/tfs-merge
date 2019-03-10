using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace TFSMergingTool.Resources
{
    /// <summary>
    /// Interaction logic for StringInputWindow.xaml
    /// </summary>
    public partial class StringInputWindow : Window
    {
        public StringInputWindow(Window owner, string question, string title, string defaultValue)
        {
            Setup(question, title, defaultValue);
            this.Owner = owner;
        }

        public StringInputWindow(string question, string title, string defaultValue)
        {
            Setup(question, title, defaultValue);
        }

        private void Setup(string question, string title, string defaultValue)
        {
            InitializeComponent();
            Title = title;
            txtQuestion.Text = question;
            txtResponse.Text = defaultValue;
            this.Loaded += StringInputWindow_Loaded;
        }

        private void StringInputWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtResponse.Focus();
        }

        public string ResponseText
        {
            get { return txtResponse.Text; }
            set { txtResponse.Text = value; }
        }

        public bool Canceled { get; set; }

        private void btnOk_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Canceled = false;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Canceled = true;
            DialogResult = false;
            Close();
        }

        public static string Show(Window owner, string question, string title, string defaultValue = "")
        {
            var inst = new StringInputWindow(owner, question, title, defaultValue);
            inst.ShowDialog();
            if (inst.DialogResult == true)
                return inst.ResponseText;
            return null;
        }

        public static string Show(string question, string title, string defaultValue = "")
        {
            var inst = new StringInputWindow(question, title, defaultValue);
            inst.ShowDialog();
            if (inst.DialogResult == true)
                return inst.ResponseText;
            return null;
        }

    }
}

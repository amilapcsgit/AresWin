using System.Windows;
using System.Windows.Input;

namespace AresWin
{
    public partial class ThemedMessageBox : Window
    {
        public ThemedMessageBox(string title, string message)
        {
            InitializeComponent();
            HeaderText.Text = title?.ToUpper() ?? "SYSTEM MESSAGE";
            MessageText.Text = message ?? "";
        }

        // Allows the user to drag the window by the header
        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static bool? Show(Window owner, string title, string message)
        {
            var dlg = new ThemedMessageBox(title, message)
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            return dlg.ShowDialog();
        }
    }
}
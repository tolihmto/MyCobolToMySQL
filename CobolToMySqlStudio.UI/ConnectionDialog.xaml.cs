using System.Windows;
using System.Windows.Controls;
using CobolToMySqlStudio.UI.ViewModels;

namespace CobolToMySqlStudio.UI
{
    public partial class ConnectionDialog : Window
    {
        public ConnectionDialog()
        {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }
    }
}

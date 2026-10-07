using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for AccountsPage.xaml
    /// </summary>
    public partial class AccountsPage
    {
        public AccountsPage()
        {
            DataContext = new AccountsViewModel();
            InitializeComponent();
        }
    }
}

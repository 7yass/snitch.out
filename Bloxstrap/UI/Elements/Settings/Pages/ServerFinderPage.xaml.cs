using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ServerFinderPage.xaml
    /// </summary>
    public partial class ServerFinderPage
    {
        public ServerFinderPage()
        {
            DataContext = new ServerFinderViewModel();
            InitializeComponent();
        }
    }
}

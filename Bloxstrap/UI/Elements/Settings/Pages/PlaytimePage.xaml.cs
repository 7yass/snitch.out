using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for PlaytimePage.xaml
    /// </summary>
    public partial class PlaytimePage
    {
        public PlaytimePage()
        {
            DataContext = new PlaytimeViewModel();
            InitializeComponent();
        }
    }
}

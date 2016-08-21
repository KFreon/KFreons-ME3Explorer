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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WPF_ME3Explorer.Textures;
using WPF_ME3Explorer.UI.ViewModels;

namespace WPF_ME3Explorer.UI
{
    /// <summary>
    /// Interaction logic for Texplorer.xaml
    /// </summary>
    public partial class Texplorer : Window
    {
        public bool IsClosed { get; private set; }
        TexplorerViewModel vm = null;
        public Texplorer()
        {
            InitializeComponent();
            vm = new TexplorerViewModel();

            vm.TreeScanProgressCloser = new Action(() =>
            {
                Storyboard closer = (Storyboard)HiderButton.Resources.FindName("TreeScanProgressPanelCloser");
                closer.Begin();
            });

            vm.TreePanelCloser = new Action(() =>
            {
                Storyboard closer = (Storyboard)TreeScanBackground.Resources["ClosePanelAnimation"];
                closer.Begin();
            });

            DataContext = vm;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }

        private async void BeginScanButton_Click(object sender, RoutedEventArgs e)
        {
            await vm.BeginTreeScan();
        }

        private void MainListView_MouseDown(object sender, MouseButtonEventArgs e)
        {

            if (e.ClickCount > 1)
            {
                vm.ShowingPreview = true;

                // Async loading
                var texInfo = (TreeTexInfo)((FrameworkElement)sender).DataContext;
                Task.Run(() => vm.LoadPreview(texInfo));
            }
            else
            {
                var texInfo = (TreeTexInfo)((FrameworkElement)sender).DataContext;
                texInfo.PopulateDetails();
            }
        }

        private void PreviewPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            vm.ShowingPreview = false;
            vm.PreviewImage = null;
        }
    }
}

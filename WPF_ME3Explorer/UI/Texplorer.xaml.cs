using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;
using UsefulThings.WPF;
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

            vm.TreePanelOpener = new Action(() =>
            {
                Storyboard opener = (Storyboard)SettingsButton.Resources["TreeScanPanelOpener"];
                opener.Begin();
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

        private async void SearchResultsItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Select folder
            TreeTexInfo tex = (TreeTexInfo)((FrameworkElement)sender).DataContext;
            TexplorerTextureFolder treeFolder = vm.AllFolders.FirstOrDefault(folder => folder.Textures.Contains(tex));
            vm.SelectedFolder = treeFolder;

            // Select texture in folder
            // Find VirtualizingWrapPanel and ensure item is in view
            while (MainDisplayPanel.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                await Task.Delay(100);

            var container = MainDisplayPanel.ItemContainerGenerator.ContainerFromIndex(0);  // First visual that exists
            var current = VisualTreeHelper.GetParent(container);
            while (current != null)
            {
                if ((current as VirtualizingWrapPanel) != null)
                    break;
                
                current = VisualTreeHelper.GetParent(current);
            }

            VirtualizingWrapPanel wrapper = current as VirtualizingWrapPanel;
            wrapper.BringItemIntoView(tex);

            // Select item
            vm.SelectedTexture = tex;

            return;
        }


        DispatcherTimer TreeSearchResetTimer = null;
        StringBuilder TreeSearchMemory = null;
        private void MainTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            // Only want letters in here.
            char key = e.Key.ToString()[0];
            if (!Char.IsLetter(key))
                return;

            if (TreeSearchMemory == null)
            {
                // Setup memory and timer
                TreeSearchMemory = new StringBuilder();
                
                if (TreeSearchResetTimer == null)
                {
                    TreeSearchResetTimer = new DispatcherTimer();
                    TreeSearchResetTimer.Interval = TimeSpan.FromSeconds(3);  // Waits x seconds before forgetting previous search
                    TreeSearchResetTimer.Tick += (unused1, unused2) =>
                    {
                        TreeSearchMemory = null;
                        TreeSearchResetTimer.Stop();
                    };
                }

                TreeSearchResetTimer.Start();
            }

            TreeSearchMemory.Append(key);  // Add to memory

            // Reset timer interval
            TreeSearchResetTimer.Stop();
            TreeSearchResetTimer.Start();


            // Peform search over top level folders only
            var topFolders = vm.TextureFolders[0].Folders;
            string temp = TreeSearchMemory.ToString();
            foreach (var folder in topFolders)
                if (folder.Name.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
                {
                    vm.SelectedFolder = folder;
                    break;
                }

            e.Handled = true;
        }

        private void ImportTreeButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = Path.GetFileName(vm.CurrentTree.TreePath);
            if (sfd.ShowDialog() == true)
                File.Copy(sfd.FileName, vm.CurrentTree.TreePath);
        }

        private void ExportTreeButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = Path.GetFileName(vm.CurrentTree.TreePath);
            if (sfd.ShowDialog() == true)
                File.Copy(vm.CurrentTree.TreePath, sfd.FileName);
        }

        private void ExportCSVButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Comma Separated File|*.CSV";
            if (sfd.ShowDialog() == true)
                vm.CurrentTree.ExportToCSV(sfd.FileName, true);
        }

        private void ReallyDeleteTreeButton_Click(object sender, RoutedEventArgs e)
        {
            vm.DeleteCurrentTree();
        }
    }
}

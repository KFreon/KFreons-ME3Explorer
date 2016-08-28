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
using Microsoft.WindowsAPICodePack.Dialogs;
using WPF_ME3Explorer.UI.ViewModels;

namespace WPF_ME3Explorer.UI
{
    /// <summary>
    /// Interaction logic for GameInformation.xaml
    /// </summary>
    public partial class GameInformation : Window
    {
        GameInformationVM vm = null;

        public GameInformation()
        {
            InitializeComponent();
        }

        public GameInformation(int version) : this()
        {
            vm = new GameInformationVM(version);
            DataContext = vm;
        }

        private void ExePathBrowser_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void BIOGamePathBrowser_Click(object sender, RoutedEventArgs e)
        {
            string newPath = PathingBrowser(false);
            vm.PathBIOGame = newPath;
        }

        private void CookedPathBrowser_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DLCPathBrowser_Click(object sender, RoutedEventArgs e)
        {

        }



        string PathingBrowser(bool isFolder)
        {
            var dialog = new CommonOpenFileDialog();

            if (isFolder)
                dialog.IsFolderPicker = true;

            var result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
                return dialog.FileName;

            return null;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            vm.SavePathing();
            this.Close();
        }
    }
}

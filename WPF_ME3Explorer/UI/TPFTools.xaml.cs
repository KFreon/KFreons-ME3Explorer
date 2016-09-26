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
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WPF_ME3Explorer.UI.ViewModels;
using UsefulThings.WPF;
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer.UI
{
    /// <summary>
    /// Interaction logic for TPFTools.xaml
    /// </summary>
    public partial class TPFTools : Window
    {
        internal TPFToolsViewModel vm = null;
        string[] AcceptedFiles = { "DirectX Images", "JPEG Images", "JPEG Images", "Bitmap Images", "PNG Images", "Targa Images", "Texmod Archives", "ME3Explorer Archives" };

        DragDropHandler<TPFTexInfo> DropHelper = null;

        public bool IsClosed { get; private set; }

        public TPFTools()
        {
            InitializeComponent();

            // Setup drag/drop handling
            var dropAction = new Action<TPFTexInfo, string[]>(async (tex, droppedFiles) => await Task.Run(() => vm.LoadFiles(droppedFiles))); // Don't need the TPFTexInfo - it'll be null anyway.
            Predicate<string[]> dropValidator = new Predicate<string[]>(files => files.All(file => TPFToolsViewModel.AcceptedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())));
            Func<TPFTexInfo, Dictionary<string, Func<byte[]>>> dataGetter = tex => new Dictionary<string, Func<byte[]>> { { tex.DefaultSaveName, () => tex.Extract() } };

            DropHelper = new DragDropHandler<TPFTexInfo>(this, dropAction, dropValidator, dataGetter);

            vm = new TPFToolsViewModel();
            DataContext = vm;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }

        private void LoadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select TPFs/images to load";
            string filter = "All Accepted files|" + String.Join("", TPFToolsViewModel.AcceptedExtensions.Select(t => "*" + t + ";")).TrimEnd(';') + "|" + String.Join("|", AcceptedFiles.Zip(TPFToolsViewModel.AcceptedExtensions, (file, ext) => file + "|*" + ext));
            ofd.Filter = filter;
            ofd.Multiselect = true;

            if (ofd.ShowDialog() == true)
                Task.Run(() => vm.LoadFiles(ofd.FileNames));
        }

        private void SavePCCsListButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();

            sfd.FileName = $"{Path.GetFileNameWithoutExtension(vm.SelectedTexture.DefaultSaveName)}_PCCs-ExpIDs.csv";
            sfd.Filter = "Comma Separated|*.csv";
            if (sfd.ShowDialog() == true)
                vm.ExportSelectedTexturePCCList(sfd.FileName);
        }

        private void MainView_Drop(object sender, DragEventArgs e)
        {
            DropHelper.Drop(sender, e);
        }

        private void MainView_DragEnter(object sender, DragEventArgs e)
        {
            // Visual effect - None?
        }

        private void MainView_DragOver(object sender, DragEventArgs e)
        {
            DropHelper.DragOver(e);
        }

        private void MainView_DragLeave(object sender, DragEventArgs e)
        {
            // Undo visual effect - none?
        }

        private void MainViewItem_MouseMove(object sender, MouseEventArgs e)
        {
            DropHelper.MouseMove(sender, e);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)   // Catch Ctrl + F
            {
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
                e.Handled = true;
            }
        }
    }
}

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

namespace WPF_ME3Explorer.UI
{
    /// <summary>
    /// Interaction logic for TPFTools.xaml
    /// </summary>
    public partial class TPFTools : Window
    {
        TPFToolsViewModel vm = null;
        string[] AcceptedFiles = { "DirectX Images", "JPEG Images", "JPEG Images", "Bitmap Images", "PNG Images", "Targa Images", "Texmod Archives", "ME3Explorer Archives" };
        string[] AcceptedExtensions = { ".dds", ".jpg", ".jpeg", ".bmp", ".png", ".tga", ".tpf", ".metpf" };

        public bool IsClosed { get; private set; }

        public TPFTools()
        {
            InitializeComponent();

            vm = new TPFToolsViewModel();
            DataContext = vm;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }

        private void LoadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select TPFs/images to load";
            string filter = "All Accepted files|" + String.Join("", AcceptedExtensions.Select(t => "*" + t + ";")).TrimEnd(';') + "|" + String.Join("|", AcceptedFiles.Zip(AcceptedExtensions, (file, ext) => file + "|*" + ext));
            ofd.Filter = filter;
            ofd.Multiselect = true;

            if (ofd.ShowDialog() == true)
                vm.LoadFiles(ofd.FileNames);
        }

        private void SavePCCsListButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();

            sfd.FileName = $"{Path.GetFileNameWithoutExtension(vm.SelectedTexture.DefaultSaveName)}_PCCs-ExpIDs.csv";
            sfd.Filter = "Comma Separated|*.csv";
            if (sfd.ShowDialog() == true)
                vm.ExportSelectedTexturePCCList(sfd.FileName);
        }
    }
}

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
        string[] AcceptedFiles = { "DirectX Images", "JPEG Images", "Bitmap Images", "PNG Images" , "Texmod Archives", "ME3Explorer Archives" };
        string[] AcceptedExtensions = { ".dds", ".jpg", ".bmp", ".png", ".tpf", ".metpf" };

        public bool IsClosed { get; private set; }

        public TPFTools()
        {
            InitializeComponent();

            vm = new TPFToolsViewModel();
            DataContext = vm;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select TPFs/images to load";
            string filter = "All Accepted files|" + String.Join("", AcceptedExtensions.Select(t => "*" + t + ";")).TrimEnd(';') + "|" + String.Join("|", AcceptedFiles.Zip(AcceptedExtensions, (file, ext) => file + "|*" + ext));
            ofd.Filter = filter;
            ofd.Multiselect = true;

            if (ofd.ShowDialog() == true)
                vm.LoadFiles(ofd.FileNames);
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            vm.ClearAll();
        }


        private void Window_Drop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];

            if (files?.Length != 0)
                vm.LoadFiles(files);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            bool enabled = true;
            if (files?.Length != 0)
            {
                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file);
                    if (!AcceptedExtensions.Contains(ext))
                    {
                        enabled = false;
                        break;
                    }
                }
            }
            else
                enabled = false;

            e.Handled = true;
            e.Effects = enabled ? DragDropEffects.All : DragDropEffects.None;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
            vm.Shutdown();
        }
    }
}

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
    }
}

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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPF_ME3Explorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public UI.TPFTools TPFToolsInstance { get; private set; }
        public UI.Texplorer TexplorerInstance { get; private set; }
        public UI.Modmaker ModmakerInstance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void TPFToolsButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TPFToolsInstance = new UI.TPFTools();
            TPFToolsInstance.Show();
        }

        private void TexplorerButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TexplorerInstance = new UI.Texplorer();
            TexplorerInstance.Show();
        }

        private void ModmakerButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ModmakerInstance = new UI.Modmaker();
            ModmakerInstance.Show();
        }
    }
}

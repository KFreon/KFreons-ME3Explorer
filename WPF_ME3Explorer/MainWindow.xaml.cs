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
using WPF_ME3Explorer.Debugging;

namespace WPF_ME3Explorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DebugOutput.StartDebugger("The Toolset");

            // Load all tools
            /*var tex = ToolsetInfo.TexplorerInstance;
            var tpf = ToolsetInfo.TPFToolsInstance;
            var mod = ToolsetInfo.ModmakerInstance;*/
        }

        private void TPFToolsButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToolsetInfo.TPFToolsInstance.Show();
        }

        private void TexplorerButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToolsetInfo.TexplorerInstance.Show();
        }

        private void ModmakerButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToolsetInfo.ModmakerInstance.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ToolsetInfo.Closing = true;
            ToolsetInfo.TPFToolsInstance?.Close();
            ToolsetInfo.TexplorerInstance?.Close();
            ToolsetInfo.ModmakerInstance?.Close();
            DebugOutput.Close();
        }
    }
}

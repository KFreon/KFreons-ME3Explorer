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
using WPF_ME3Explorer.Textures;

namespace WPF_ME3Explorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int UI_DelayTime = 100;

        public MainWindow()
        {
            InitializeComponent();
            DebugOutput.StartDebugger("The Toolset");

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MessageBox.Show(
                    "An unhandled exception has occured and the application will close." + Environment.NewLine + 
                    "Take note of the reason below, as it may help the developer." + Environment.NewLine + Environment.NewLine + 
                    args.ExceptionObject.ToString());
                Application.Current.Shutdown();
            };

            Startup();
        }

        async void Startup()
        {
            IsEnabled = false;

            // Load slow bits
            await ToolsetTextureEngine.Initialise();
            /*var tex = ToolsetInfo.TexplorerInstance;
            var tpf = ToolsetInfo.TPFToolsInstance;
            var mod = ToolsetInfo.ModmakerInstance;*/

            IsEnabled = true;
        }

        private async void TPFToolsButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToolsetInfo.TPFToolsInstance.vm.Busy = true;
            ToolsetInfo.TPFToolsInstance.Show();

            await Task.Delay(UI_DelayTime);

            ToolsetInfo.TPFToolsInstance.vm.Busy = false;
        }

        private async void TexplorerButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToolsetInfo.TexplorerInstance.vm.Busy = true;
            ToolsetInfo.TexplorerInstance.Show();
            ToolsetInfo.TexplorerInstance.vm.Refresh();

            await Task.Delay(UI_DelayTime);

            ToolsetInfo.TexplorerInstance.vm.Busy = false;  // Needed as when form is shown, mouse can be over a button and trigger the button's command.
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

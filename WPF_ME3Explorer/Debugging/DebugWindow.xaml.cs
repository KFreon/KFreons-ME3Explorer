using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace WPF_ME3Explorer.Debugging
{
    /// <summary>
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public partial class DebugWindow : Window
    {
        public DebugWindow()
        {
            InitializeComponent();
            DebugOutput.SetBox(rtb, DebugScroller);
        }

        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Select destination for Debug Log";
            sfd.FileName = $"ME3Explorer Debug Log - {DateTime.Now.Date}.txt";
            sfd.AddExtension = true;

            Thread thread = new Thread(() =>
            {
                if (sfd.ShowDialog() == true)
                {
                    string error = DebugOutput.Save(sfd.FileName);
                    if (error == null)
                        MessageBox.Show($"Saved contents to {sfd.FileName}");
                    else
                        MessageBox.Show($"Failed to save contents. Reason: {error}");
                }

                System.Windows.Threading.Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }
}

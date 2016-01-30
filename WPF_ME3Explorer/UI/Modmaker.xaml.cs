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

namespace WPF_ME3Explorer.UI
{
    /// <summary>
    /// Interaction logic for Modmaker.xaml
    /// </summary>
    public partial class Modmaker : Window
    {
        public bool IsClosed { get; private set; }
        public Modmaker()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }
    }
}

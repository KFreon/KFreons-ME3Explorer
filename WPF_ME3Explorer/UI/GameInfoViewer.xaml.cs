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
using WPF_ME3Explorer.UI.ViewModels;

namespace WPF_ME3Explorer.UI
{
    /// <summary>
    /// Interaction logic for GameInfoViewer.xaml
    /// </summary>
    public partial class GameInfoViewer : Window
    {
        GameInfoViewModel vm = null;
        public GameInfoViewer()
        {
            InitializeComponent();
        }

        public GameInfoViewer(string title, int game) : this()
        {
            vm = new GameInfoViewModel(title, game);
            DataContext = vm;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            vm.Save();
            DialogResult = true;
            Close();
        }
    }
}

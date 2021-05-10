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
using ylccProtocol;
using Grpc.Net.Client;

namespace ylcGroupingClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Setting _setting = new Setting();
        private readonly YlccProtocol protocol = new YlccProtocol();

        public MainWindow()
        {
            InitializeComponent();
            VideoIdTextBox.DataContext = _setting;
            ChoicesDataGrid.DataContext = _setting;
            TargetComboBox.DataContext = _setting;
            URITextBox.DataContext = _setting;
            InsecureCheckBox.DataContext = _setting;
            MaxHeightTextBox.DataContext = _setting;
            MaxWidthTextBox.DataContext = _setting;
            WindowBackgroundColorTextBox.DataContext = _setting;
            WindowBackgroundColorBorder.DataContext = _setting;
            BoxForegroundColorTextBox.DataContext = _setting;
            BoxForegroundColorBorder.DataContext = _setting;
            BoxBackgroundColorTextBox.DataContext = _setting;
            BoxBackgroundColorBorder.DataContext = _setting;
            BoxBorderColorTextBox.DataContext = _setting;
            BoxBorderColorBorder.DataContext = _setting;
            FontSizeTextBox.DataContext = _setting;
            PaddingTextBox.DataContext = _setting;
        }
        private void AddChoiceButtonClick(object sender, RoutedEventArgs e)
        {
            if (ChoicesTextBox.Text == null || ChoicesTextBox.Text == "")
            {
                return;
            }
            _setting.Choices.Add(new Choice() { Text = ChoicesTextBox.Text });
            ChoicesTextBox.Text = "";
        }

        private void ChoiceRemove(object sender, RoutedEventArgs e)
        {
            if (ChoicesDataGrid.SelectedIndex == -1)
            {
                return;
            }
            _setting.Choices.Remove(_setting.Choices[ChoicesDataGrid.SelectedIndex]);
            ChoicesDataGrid.SelectedIndex = -1;
        }

        private void StartGroupingClick(object sender, RoutedEventArgs e)
        {
            if (_setting.VideoId == null || _setting.VideoId == "")
            {
                MessageBox.Show("VideoIdが入力されていません");
                return;
            }
            if (_setting.Choices.Count == 0)
            {
                MessageBox.Show("選択肢が入力されていません");
                return;
            }
            ViewWindow viewWindow = new ViewWindow();
            viewWindow.Show();
            viewWindow.StartGrouping(_setting);
        }
    }
}

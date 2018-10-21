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

namespace SMDocClient
{
    /// <summary>
    /// Signup.xaml 的交互逻辑
    /// </summary>
    public partial class Signup : Window
    {
        public string name;
        public Signup()
        {
            InitializeComponent();
        }

        private void Sure(object sender, RoutedEventArgs e)
        {
            if (textBox.Text == "")
            {
                MessageBox.Show("识别名称不能为空");
                return;
            }
            name = textBox.Text;
            if (name.Length > 12)
            {
                name = name.Substring(0, 12);
            }
            DialogResult = true;
            Close();
        }
    }
}

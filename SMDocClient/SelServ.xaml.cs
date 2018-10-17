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
    /// SelServ.xaml 的交互逻辑
    /// </summary>
    public partial class SelServ : Window
    {
        public ShowItem current;
        private ServFinder serv;
        private ServFinder.Renew renew;
        public SelServ(ServFinder finder)
        {
            serv = finder;
            InitializeComponent();
            renew = new ServFinder.Renew(Serv_renew);
            serv.renew += renew;
            renew();
        }

        private void Serv_renew()
        {
            Dispatcher.Invoke(() =>
            {
                var list = serv.GetList();
                listBox.Items.Clear();
                foreach (var it in list)
                {
                    listBox.Items.Add(new ShowItem { name = it.name, addr = it.address });
                }
            });
        }

        private void Sure(object sender, RoutedEventArgs e)
        {
            if (listBox.SelectedItem == null)
            {
                MessageBox.Show("请选择一个目标");
                return;
            }
            current = (ShowItem)listBox.SelectedItem;
            DialogResult = true;
            serv.renew -= renew;
            Close();
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            serv.renew -= renew;
            Close();
        }

        private void TryClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            serv.renew -= renew;
        }

        public class ShowItem
        {
            public string name;
            public string addr;
            public override string ToString()
            {
                return name;
            }
        }
    }
}

using Microsoft.Win32;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SMDocClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        ClientLib ctx = new ClientLib();
        ServFinder serv;
        string sign_name;
        ClientLib.Conn conn = null;
        Thread workTh = null;
        bool doCacnel = false;

        public MainWindow()
        {
            InitializeComponent();
            Signup signup = new Signup();
            bool? result = signup.ShowDialog();
            if (result != null && result.Value)
            {
                sign_name = signup.name;
                serv = new ServFinder();
                serv.start();
                statusBox.Text += "SM4文件加密服务 v1.0\n";
            }
            else
            {
                Close();
            }
        }

        private SelServ.ShowItem switchServ()
        {
            SelServ sel = new SelServ(serv);
            bool? result = sel.ShowDialog();
            if (result != null && result.Value)
            {
                return sel.current;
            }
            return null;
        }

        private void SelInFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofs = new OpenFileDialog();
            ofs.Multiselect = false;
            bool? result = ofs.ShowDialog();
            if (result != null && result.Value)
            {
                inputFile.Text = ofs.FileName;
            }
        }

        private void SelOutFile(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfs = new SaveFileDialog();
            bool? result = sfs.ShowDialog();
            if (result != null && result.Value)
            {
                outputFile.Text = sfs.FileName;
            }
        }

        private void EncReq(object sender, RoutedEventArgs e)
        {
            if (inputFile.Text == "")
            {
                statusBox.Text += "未选择输入文件\n";
                MessageBox.Show("请选择输入文件");
                return;
            }
            if (outputFile.Text == "")
            {
                outputFile.Text = inputFile.Text + ".sdc";//sm document cipher
            }
            doCacnel = false;
            inputBtn.IsEnabled = false;
            outputBtn.IsEnabled = false;
            inputFile.IsReadOnly = true;
            outputFile.IsReadOnly = true;
            encBtn.IsEnabled = false;
            decBtn.IsEnabled = false;
            cancel.IsEnabled = true;
            statusBox.Text += "选择加密源……\n";
            var sel = switchServ();
            if (sel == null)
            {
                statusBox.Text += "已取消操作\n";
                tryCancel();
                return;
            }
            statusBox.Text += "选择: " + sel.name + "\n";
            statusBox.Text += "建立连接……\n";
            conn = new ClientLib.Conn(sel.addr);
            workTh = ctx.CalcHash(inputFile.Text, EncHashCallback);
            statusBox.Text += "计算Hash……\n";
        }

        private void DecReq(object sender, RoutedEventArgs e)
        {
            if (inputFile.Text == "")
            {
                statusBox.Text += "未选择输入文件\n";
                MessageBox.Show("请选择输入文件");
                return;
            }
            if (outputFile.Text == "")
            {
                string s = inputFile.Text;
                if (s.Substring(s.Length - 4) == ".sdc")
                {
                    outputFile.Text = s.Substring(0, s.Length - 4);
                }
                outputFile.Text = inputFile.Text + ".sdr";//sm document raw
            }
            doCacnel = false;
            inputBtn.IsEnabled = false;
            outputBtn.IsEnabled = false;
            inputFile.IsReadOnly = true;
            outputFile.IsReadOnly = true;
            encBtn.IsEnabled = false;
            decBtn.IsEnabled = false;
            cancel.IsEnabled = true;
            statusBox.Text += "选择解密源……\n";
            var sel = switchServ();
            if (sel == null)
            {
                statusBox.Text += "已取消操作\n";
                tryCancel();
                return;
            }
            statusBox.Text += "选择: " + sel.name + "\n";
            statusBox.Text += "建立连接……\n";
            conn = new ClientLib.Conn(sel.addr);
            workTh = ctx.CalcHash(inputFile.Text, DecHashCallback);
            statusBox.Text += "计算Hash……\n";
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            tryCancel();
            statusBox.Text += "操作已取消\n";
        }

        private void tryCancel()
        {
            doCacnel = true;
            if (conn != null)
            {
                conn.Close();
                conn = null;
            }
            if (workTh != null)
            {
                workTh.Abort();
                workTh = null;
            }
            inputBtn.IsEnabled = true;
            outputBtn.IsEnabled = true;
            inputFile.IsReadOnly = false;
            outputFile.IsReadOnly = false;
            encBtn.IsEnabled = true;
            decBtn.IsEnabled = true;
            cancel.IsEnabled = false;
        }

        private void EncHashCallback(byte[] hash)
        {
            if (doCacnel) return;
            Dispatcher.Invoke(() =>
            {
                if (doCacnel) return;
                if (hash == null)
                {
                    statusBox.Text += "Hash计算失败\n";
                    tryCancel();
                    return;
                }
                statusBox.Text += "Hash计算完成\n等待手机同意……\n";
                workTh = conn.ReqEnc(sign_name + "\n" + System.IO.Path.GetFileName(inputFile.Text), hash, EncKeyIvCallback);
            });
        }

        private void DecHashCallback(byte[] hash)
        {
            if (doCacnel) return;
            Dispatcher.Invoke(() =>
            {
                if (doCacnel) return;
                if (hash == null)
                {
                    statusBox.Text += "Hash计算失败\n";
                    tryCancel();
                    return;
                }
                statusBox.Text += "Hash计算完成\n等待手机同意……\n";
                workTh = conn.ReqDec(sign_name + "\n" + System.IO.Path.GetFileName(inputFile.Text), hash, DecKeyIvCallback);
            });
        }

        private void EncKeyIvCallback(byte[] key, byte[] iv, byte[] hash)
        {
            if (doCacnel) return;
            Dispatcher.Invoke(() =>
            {
                if (doCacnel) return;
                if (key == null)
                {
                    statusBox.Text += "手机已拒绝\n";
                    tryCancel();
                }
                else
                {
                    statusBox.Text += "开始加密……\n";
                }
            });
            if (key == null) return;
            byte[] rethash = ctx.Enc(inputFile.Text, outputFile.Text, key, iv);
            if (rethash == null)
            {
                Dispatcher.Invoke(() =>
                {
                    statusBox.Text += "加密失败\n";
                    tryCancel();
                });
            }
            else
            {
                if (!conn.SendHash(rethash))
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusBox.Text += "手机失去连接，加密失败\n";
                        tryCancel();
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusBox.Text += "加密完成\n";
                        statusBox.Text += inputFile.Text + " -> " + outputFile.Text + "\n";
                        tryCancel();
                    });
                }
            }
        }

        private void DecKeyIvCallback(byte[] key, byte[] iv, byte[] hash)
        {
            if (doCacnel) return;
            Dispatcher.Invoke(() =>
            {
                if (doCacnel) return;
                if (key == null)
                {
                    statusBox.Text += "手机已拒绝\n";
                    tryCancel();
                }
                else
                {
                    statusBox.Text += "开始解密……\n";
                }
            });
            if (key == null) return;
            byte[] rethash = ctx.Dec(inputFile.Text, outputFile.Text, key, iv);
            if (rethash == null)
            {
                Dispatcher.Invoke(() =>
                {
                    statusBox.Text += "解密失败\n";
                    tryCancel();
                });
            }
            else
            {
                if (!Enumerable.SequenceEqual(hash, rethash))
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusBox.Text += "校验错误，解密失败\n";
                        tryCancel();
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusBox.Text += "解密完成\n";
                        statusBox.Text += inputFile.Text + " -> " + outputFile.Text + "\n";
                        tryCancel();
                    });
                }
            }
        }
    }
}

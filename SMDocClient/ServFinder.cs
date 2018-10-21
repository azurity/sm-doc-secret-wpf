using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SMDocClient
{
    public class ServFinder
    {
        UdpClient socket;
        bool close = false;
        volatile List<Item> servlist;
        Thread th = null;
        Thread timeCheck = null;

        public ServFinder()
        {
            servlist = new List<Item>();
            socket = new UdpClient(9001);
        }

        ~ServFinder()
        {
            timeCheck.Abort();
            th.Abort();
        }

        public delegate void Renew();
        public event Renew renew;

        public void start()
        {
            if (th != null)
            {
                th.Abort();
            }
            if (timeCheck != null)
            {
                timeCheck.Abort();
            }
            close = false;
            th = new Thread(() =>
            {
                try
                {
                    IPEndPoint point = new IPEndPoint(IPAddress.Any, 0);
                    while (!close && socket != null)
                    {
                        socket.BeginReceive(delegate (IAsyncResult result)
                        {
                            string info = (string)result.AsyncState;
                            string addr = point.Address.ToString();
                            string[] strArr = info.Split('\n');
                            if (strArr.Length != 2 || strArr[0] != "SM-DOC-SERV")
                            {
                                return;
                            }
                            add(strArr[1], addr);
                            renew?.Invoke();
                        }, Encoding.UTF8.GetString(socket.Receive(ref point)));
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
            });
            th.IsBackground = true;
            th.Start();
            timeCheck = new Thread(() =>
            {
                Thread.Sleep(new TimeSpan(0, 0, 5));
                timeOut();
            });
            timeCheck.IsBackground = true;
            timeCheck.Start();
            return;
        }

        public void stop()
        {
            close = true;
        }

        private void add(string name, string addr)
        {
            lock (servlist)
            {
                servlist.RemoveAll((Item it) =>
                {
                    return (it.name == name || it.address == addr);
                });
                servlist.Add(new Item { name = name, address = addr, time = DateTime.UtcNow });
            }
        }

        public List<Item> GetList()
        {
            List<Item> ret = null;
            lock (servlist)
            {
                ret = new List<Item>();
                foreach (var it in servlist)
                {
                    ret.Add(new Item { name = it.name, address = it.address });
                }
            }
            return ret;
        }

        private void timeOut()
        {
            int count = 0;
            lock (servlist)
            {
                count = servlist.RemoveAll((Item it) =>
                {
                    return (DateTime.UtcNow - it.time).TotalSeconds > 120;
                });
            }
            if (count > 0)
            {
                renew?.Invoke();
            }
        }

        public class Item
        {
            public string name = "missing name";
            public string address = "unknown";
            public DateTime time;
        }
    }
}

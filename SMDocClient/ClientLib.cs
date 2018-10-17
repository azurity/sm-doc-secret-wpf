using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SMDocClient
{
    public class ClientLib
    {
        public ClientLib()
        {
            CreateEnv();
        }

        ~ClientLib()
        {
            ReleaseEnv();
        }

        public delegate void HashCallback(byte[] hash);
        public delegate void CryptoCallback(byte[] hash);

        public Thread CalcHash(string filepath, HashCallback callback)
        {
            Thread th = new Thread(() =>
            {
                byte[] hash = new byte[32];
                if(!fileHash(filepath, hash))
                {
                    callback?.Invoke(null);
                    return;
                }
                callback?.Invoke(hash);
            });
            th.Start();
            return th;
        }

        public byte[] Enc(string inFilePath, string outFilePath, byte[] key, byte[] iv)
        {
            byte[] hash = new byte[32];
            encfile(inFilePath, outFilePath, key, iv, hash);
            return hash;
        }

        public byte[] Dec(string inFilePath, string outFilePath, byte[] key, byte[] iv)
        {
            byte[] hash = new byte[32];
            decfile(inFilePath, outFilePath, key, iv, hash);
            return hash;
        }

        #region CppNative
        [DllImport("Clientlib.dll", EntryPoint = "CreateEnv")]
        private extern static bool CreateEnv();

        [DllImport("Clientlib.dll", EntryPoint = "ReleaseEnv")]
        private extern static void ReleaseEnv();

        [DllImport("Clientlib.dll", EntryPoint = "CreateConn")]
        private extern static IntPtr CreateConn(string addr);

        [DllImport("Clientlib.dll", EntryPoint = "CloseConn")]
        private extern static void CloseConn(IntPtr conn);

        [DllImport("Clientlib.dll", EntryPoint = "SendPack")]
        private extern static bool SendPack(IntPtr conn, byte[] buffer);

        [DllImport("Clientlib.dll", EntryPoint = "RecvPack")]
        private extern static bool RecvPack(IntPtr conn, byte[] buffer);

        [DllImport("Clientlib.dll", EntryPoint = "getError")]
        private extern static string getError();

        [DllImport("Clientlib.dll", EntryPoint = "fileHash")]
        private extern static bool fileHash(string filePath, byte[] hash);

        [DllImport("Clientlib.dll", EntryPoint = "encfile")]
        private extern static bool encfile(string inFilePath, string outFilePath, byte[] key, byte[] iv, byte[] hash);

        [DllImport("Clientlib.dll", EntryPoint = "decfile")]
        private extern static bool decfile(string inFilePath, string outFilePath, byte[] key, byte[] iv, byte[] hash);
        #endregion

        public class Conn
        {
            public delegate void KeyIvCallback(byte[] key, byte[] iv, byte[] hash);
            private IntPtr conn = IntPtr.Zero;
            private bool close = false;
            private Thread heartTh = null;

            public Conn(string addr)
            {
                conn = CreateConn(addr);
                heartTh = new Thread(heart);
                heartTh.Start();
            }

            ~Conn()
            {
                close = true;
                if (conn != IntPtr.Zero) CloseConn(conn);
                if (heartTh != null)
                {
                    heartTh.Abort();
                }
                conn = IntPtr.Zero;
            }

            private void heart()
            {
                byte[] heartBuf = new byte[256];
                Array.Clear(heartBuf, 0, 256);
                heartBuf[0] = 0x0f;
                try
                {
                    while (!close)
                    {
                        Thread.Sleep(20 * 1000);
                        if(!SendPack(conn, heartBuf))
                        {
                            break;
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
            }

            public void Close()
            {
                close = true;
                if (conn != IntPtr.Zero) CloseConn(conn);
                conn = IntPtr.Zero;
            }

            public Thread ReqEnc(string info, byte[] hash, KeyIvCallback keyiv)
            {
                if (conn == null) return null;
                byte[] buffer = new byte[256];
                Array.Clear(buffer, 0, 256);
                buffer[0] = 1;
                hash.CopyTo(buffer, 4);
                Encoding.UTF8.GetBytes(info).CopyTo(buffer, 36);
                Thread th = new Thread(() =>
                {
                    if(!SendPack(conn, buffer))
                    {
                        keyiv?.Invoke(null, null, null);
                        return;
                    }
                    if(!RecvPack(conn, buffer))
                    {
                        keyiv?.Invoke(null, null, null);
                        return;
                    }
                    if (buffer[0] != 3)
                    {
                        keyiv?.Invoke(null, null, null);
                        return;
                    }
                    //success
                    byte[] key = new byte[16];
                    byte[] iv = new byte[16];
                    byte[] cbhash = new byte[32];
                    Array.Copy(buffer, 4, key, 0, 16);
                    Array.Copy(buffer, 20, iv, 0, 16);
                    Array.Copy(buffer, 36, cbhash, 0, 32);
                    keyiv?.Invoke(key, iv, cbhash);
                });
                th.Start();
                return th;
            }

            public Thread ReqDec(string info, byte[] hash, KeyIvCallback keyiv)
            {
                if (conn == null) return null;
                byte[] buffer = new byte[256];
                Array.Clear(buffer, 0, 256);
                buffer[0] = 2;
                hash.CopyTo(buffer, 4);
                Encoding.UTF8.GetBytes(info).CopyTo(buffer, 36);
                Thread th = new Thread(() =>
                {
                    if(!SendPack(conn, buffer))
                    {
                        keyiv?.Invoke(null, null, null);
                        return;
                    }
                    if(!RecvPack(conn, buffer))
                    {
                        keyiv?.Invoke(null, null, null);
                        return;
                    }
                    if (buffer[0] != 3)
                    {
                        keyiv?.Invoke(null, null, null);
                        return;
                    }
                    //success
                    byte[] key = new byte[16];
                    byte[] iv = new byte[16];
                    byte[] cbhash = new byte[32];
                    Array.Copy(buffer, 4, key, 0, 16);
                    Array.Copy(buffer, 20, iv, 0, 16);
                    Array.Copy(buffer, 36, cbhash, 0, 32);
                    keyiv?.Invoke(key, iv, cbhash);
                });
                th.Start();
                return th;
            }

            public bool SendHash(byte[] hash)
            {
                byte[] buffer = new byte[256];
                Array.Clear(buffer, 0, 256);
                buffer[0] = 5;
                hash.CopyTo(buffer, 4);
                return SendPack(conn, buffer);
            }
        }
    }
}

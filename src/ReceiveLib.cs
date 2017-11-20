using System;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace ReceiveLib
{
    /// <summary>
    /// メッセージの受信を行います
    /// </summary>
    public class StringIoReceive
    {
        private bool startTf = false;
        private int tPortNumber;
        /// <summary>
        ///使用できるポート番号を取得します
        /// </summary>
        public int Getport()
        {
            string command = "portnum.bat";
            ProcessStartInfo psInfo = new ProcessStartInfo();
            psInfo.FileName = command;
            psInfo.CreateNoWindow = true;
            psInfo.UseShellExecute = false;
            psInfo.RedirectStandardOutput = true;
            for (int i = 49152; i <= 65535; i++)
            {
                psInfo.Arguments = i.ToString();
                Process p = Process.Start(psInfo);
                string output = p.StandardOutput.ReadToEnd();
                if (output.Length == 0) return i;
            }
            return -1;
        }
        /// <summary>
        /// サーバーの動作を開始します
        /// </summary>
        public void Start(int PortNumber)
        {
            if (startTf == true) return;
            tPortNumber = PortNumber;
            var tsk = new Thread(ReceiveIo);
            tsk.Start();
            startTf = true;
        }

        private void ReceiveIo()
        {
            var receive = new TcpListener(System.Net.IPAddress.Any, tPortNumber);
            receive.Start();

            while (true)
            {
                TcpClient cl = new TcpClient();
                cl = receive.AcceptTcpClient();

                var handle = new clsChild(cl);
                handle.ReceivedMessage += handle_ReceivedMessage;
                handle.LostConnectedPeer += handle_LostConnectedPeer;
                var tsk = new Thread(handle.task);
                tsk.Start();
                var e = new IPEventArgs();
                IPAddress ipa = IPAddressConvert.GetRemoteIPAddress(cl);
                string portnum = IPAddressConvert.GetRemotePortNumber(cl);

                e.IPAddress = ipa;
                e.PortNumber = portnum;

                ConnectedPeerHandler(e);
            }
        }
        /// <summary>
        /// メッセージが飛んできたら
        /// </summary>
        void handle_ReceivedMessage(object sender, MessageEventArgs e)
        {
            //更にメッセージプッシュ
            ReceivedMessageHandler(e);
        }
        void handle_LostConnectedPeer(object sender, IPEventArgs e)
        {
            LostConnectedPeerHandler(e);
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        //イベント
        public delegate void MessageEventHandler(object sender, MessageEventArgs e);
        public event MessageEventHandler ReceivedMessage;
        protected virtual void ReceivedMessageHandler(MessageEventArgs e)
        {
            if (ReceivedMessage != null)
            {
                ReceivedMessage(this, e);
            }
        }

        public delegate void ConnectEventHandler(object sender, IPEventArgs e);
        public event ConnectEventHandler ConnectedPeer;
        protected virtual void ConnectedPeerHandler(IPEventArgs e)
        {
            if (ConnectedPeer != null)
            {
                ConnectedPeer(this, e);
            }
        }

        public delegate void LostConnectEventHandler(object sender, IPEventArgs e);
        public event LostConnectEventHandler LostConnectedPeer;
        protected virtual void LostConnectedPeerHandler(IPEventArgs e)
        {
            if (LostConnectedPeer != null)
            {
                LostConnectedPeer(this, e);
            }
        }

        /// <summary>
        /// 相手ごとにメッセージ受信を行います
        /// </summary>
        private class clsChild
        {
            private TcpClient soc;
            public clsChild(TcpClient cl)
            {
                soc = cl;
            }

            public void task()
            {
                IPAddress ipa = IPAddressConvert.GetRemoteIPAddress(soc);
                string portnum = IPAddressConvert.GetRemotePortNumber(soc);
                try
                {
                    NetworkStream s = soc.GetStream();

                    string str = "";
                    byte[] buf = new byte[4000 * 3000 * 3];
                    int len = s.Read(buf, 0, buf.Length);
                    while (len > 0)
                    {
                        var tbuf = new byte[buf.Length - 1];
                        for (int i = 0; i < buf.Length - 1; i++)
                        {
                            tbuf[i] = buf[i + 1];
                        }
                        if (buf[0] == (byte)'i')//イメージ
                        {
                            var img = ByteArrayToImage(tbuf);
                            var e = new MessageEventArgs();
                            e.Type = MessageType.Image;
                            e.Image = img;
                            e.Message = null;
                            e.IPAddress = ipa;
                            e.PortNumber = portnum;
                            ReceivedMessageHandler(e);
                        }
                        else if (buf[0] == (byte)'m')//メッセージ
                        {
                            str = Encoding.UTF8.GetString(tbuf, 0, len - 1);
                            var e = new MessageEventArgs();
                            e.Type = MessageType.Message;
                            e.Image = null;
                            e.Message = str;
                            e.IPAddress = ipa;
                            ReceivedMessageHandler(e);
                        }
                        len = s.Read(buf, 0, buf.Length);
                    }
                    var e2 = new IPEventArgs();
                    e2.IPAddress = ipa;
                    LostConnectedPeerHandler(e2);
                }
                catch (Exception)
                {
                    var e2 = new IPEventArgs();
                    e2.IPAddress = ipa;
                    LostConnectedPeerHandler(e2);
                }
            }

            public static Image ByteArrayToImage(byte[] b)
            {
                ImageConverter imgconv = new ImageConverter();
                Image img = (Image)imgconv.ConvertFrom(b);
                return img;
            }

            //イベント
            public delegate void MessageEventHandler(object sender, MessageEventArgs e);
            public event MessageEventHandler ReceivedMessage;

            protected virtual void ReceivedMessageHandler(MessageEventArgs e)
            {
                if (ReceivedMessage != null)
                {
                    ReceivedMessage(this, e);
                }
            }

            public delegate void LostConnectEventHandler(object sender, IPEventArgs e);
            public event LostConnectEventHandler LostConnectedPeer;
            protected virtual void LostConnectedPeerHandler(IPEventArgs e)
            {
                if (LostConnectedPeer != null)
                {
                    LostConnectedPeer(this, e);
                }
            }
        }
    }

    /// <summary>
    /// 送られてきたメッセージのタイプを定義します
    /// </summary>
    public enum MessageType
    {
        /// <summary>イメージ画像を受信しました</summary>
        Image,
        /// <summary>メッセージを受信しました</summary>
        Message
    }

    public class MessageEventArgs : EventArgs
    {
        public MessageType Type;
        public string Message;
        public Image Image;
        public IPAddress IPAddress;
        public string PortNumber;
    }

    public class IPEventArgs : EventArgs
    {
        public IPAddress IPAddress;
        public string PortNumber;
    }

    public static class IPAddressConvert
    {
        public static IPAddress GetRemoteIPAddress(TcpClient client)
        {
            return ((IPEndPoint)client.Client.RemoteEndPoint).Address;
        }
        public static IPAddress GetRemoteIPAddress(Socket client)
        {
            return ((IPEndPoint)client.RemoteEndPoint).Address;
        }
        public static string GetRemotePortNumber(TcpClient client)
        {
            return ((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString();
        }
        public static string GetRemotePortNumber(Socket client)
        {
            return ((IPEndPoint)client.RemoteEndPoint).Port.ToString();
        }
    }
}

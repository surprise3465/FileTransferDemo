using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace WpfSocketDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private string path = AppDomain.CurrentDomain.BaseDirectory;

        Socket serverSock = null;
        Socket clientSock = null;

        ObservableCollection<SocketFileInfo> sendFiles = new ObservableCollection<SocketFileInfo>();
        ObservableCollection<SocketFileInfo> recvFiles = new ObservableCollection<SocketFileInfo>();

        private readonly object locker = new object();

        private Thread sendThread = null;

        private string ipaddr = "127.0.0.1";

        //Send Files
        private ushort localSendPort = 20041;//computer send port
        private ushort remoteRecvPort = 30041;// device recv port

        public MainWindow()
        {
            InitializeComponent();
            IPAddr iPAddr = new IPAddr();
            if (iPAddr.ShowDialog() == true)
            {
                ipaddr = iPAddr.ipaddr;
                InitSocketAndThread();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void InitSocketAndThread()
        {
            sendFileDataGrid.ItemsSource = sendFiles;
            recvFileDataGrid.ItemsSource = recvFiles;

            clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSock.Bind(new IPEndPoint(IPAddress.Parse(ipaddr), localSendPort));  //绑定IP地址：端口
            
            sendThread = new Thread(SendFilesThreadFunc)
            { IsBackground = true, };
        }

        private void SendFilesThreadFunc()
        {
            while (true)
            {
                Thread.Sleep(2000);
                Monitor.Enter(locker);
                foreach (var item in sendFiles)
                {
                    if (item.TaskSts == TaskStatus.NONE)
                    {
                        SendFileToDevice(item);
                        break;
                    }
                }
                Monitor.Exit(locker);
            }
        }

        private void SendFileToDevice(SocketFileInfo item)
        {
            try
            {
                int BufferSize = 1024;              
                byte[] buffer = new byte[256];
                byte[] fileBuffer = new byte[BufferSize];

                using (FileStream reader = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    long send = 0L;
                    long length = reader.Length;

                    string fileName = Path.GetFileName(item.FilePath);
                    string sendStr = "Req=1001|" + "FileName=" + fileName + "|" + "FileSize=" + length.ToString();
                    clientSock.Send(Encoding.Default.GetBytes(sendStr));
      
                    clientSock.Receive(buffer);
                    string mes = Encoding.Default.GetString(buffer);

                    if (mes.Contains("OK"))
                    {
                        Console.WriteLine("Sending file:" + fileName + ".Plz wait...");
                        int read, sent;
                        while ((read = reader.Read(fileBuffer, 0, BufferSize)) != 0)
                        {
                            sent = 0;
                            while ((sent += clientSock.Send(fileBuffer, sent, read, SocketFlags.None)) < read)
                            {
                                send += (long)sent;
                            }
                        }
                        Console.WriteLine("Send finish.\n");
                    }
                    item.TaskSts = TaskStatus.SUCCESS;
                }
                
                int count = clientSock.Receive(buffer);
                string[] command = Encoding.UTF8.GetString(buffer, 0, count).Split('|');
                Console.WriteLine("收到" + Encoding.UTF8.GetString(buffer, 0, count));
                if (command[0] == "ACK=1001")
                {
                    string fileName = command[1].Replace("FileName=", "");
                    var fileSize = Convert.ToInt64(command[2].Replace("FileSize=", ""));
                    clientSock.Send(Encoding.UTF8.GetBytes("ACK=OK"));
                    SocketFileInfo socketFileInfo = new SocketFileInfo()
                    { FilePath = path + fileName, TimeStamp = DateTime.Now.ToString(), FileSize = fileSize, TaskSts = TaskStatus.NONE };
                    long receive = 0L;
                    using (FileStream writer = new FileStream(Path.Combine(path, socketFileInfo.FilePath), FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int received;
                        while (receive < socketFileInfo.FileSize)
                        {
                            received = clientSock.Receive(buffer);
                            writer.Write(buffer, 0, received);
                            writer.Flush();
                            receive += (long)received;
                        }
                    }
                    socketFileInfo.FilePath = Path.Combine(path, socketFileInfo.FilePath);
                    Console.WriteLine("Receive finish.\n");
                    this.Dispatcher.Invoke(new Action(() => { recvFiles.Add(socketFileInfo); }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
   
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            var res = openFileDialog.ShowDialog();
            if ((bool)res)
            {
                textBox.Text = openFileDialog.FileName;
                SocketFileInfo socketFileInfo = new SocketFileInfo()
                {
                    TaskSts = TaskStatus.NONE,
                    FilePath = openFileDialog.FileName,
                    TimeStamp = DateTime.Now.ToString(),
                    FileSize = (int)new FileInfo(openFileDialog.FileName).Length
                };
                sendFiles.Add(socketFileInfo);
            }
        }

        private void btnTryMsg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int BufferSize = 1024;
                byte[] buffer = new byte[BufferSize];
                string sendStr = "Req=1002|Content=TryMsg";
                clientSock.Send(Encoding.UTF8.GetBytes(sendStr));
                int count = clientSock.Receive(buffer);
                var res = Encoding.UTF8.GetString(buffer, 0, count);
                errorSts.Text = res;
            }
            catch (SocketException EX)
            {
                infoSts.Text = EX.ToString();
            }
        }

        private void btnTryConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sendThread != null && sendThread.IsAlive)
                {
                    sendThread.Abort();
                    sendThread = null;
                    clientSock.Close();
                    clientSock = null;
                }
                else
                {
                    sendThread = new Thread(SendFilesThreadFunc);
                    sendThread.Start();

                    clientSock.Connect(IPAddress.Parse("192.168.0.102"), remoteRecvPort);
                }          
            }
            catch (SocketException ex)
            {
                infoSts.Text = ex.ToString();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            clientSock.Close();
            System.Environment.Exit(0);
        }
    }

    public class SocketFileInfo : INotifyPropertyChanged
    {
        private string timeStamp;
        private string filePath;
        public string TimeStamp
        {
            get
            {
                return timeStamp;
            }
            set
            {
                timeStamp = value;
                OnPropertyChanged("TimeStamp");
            }
        }
        public string FilePath
        {
            get
            {
                return filePath;
            }
            set
            {
                filePath = value;
                OnPropertyChanged("FilePath");
            }
        }

        public string TaskStsStr { get; set; }
        private TaskStatus _taskSts = TaskStatus.NONE;
        public TaskStatus TaskSts
        {
            get
            {
                return _taskSts;
            }
            set
            {
                _taskSts = value;
                if (_taskSts == TaskStatus.NONE)
                {
                    TaskStsStr = "Unstart";
                }
                else if (_taskSts == TaskStatus.WORKING)
                {
                    TaskStsStr = "In Process";
                }
                else if (_taskSts == TaskStatus.SUCCESS)
                {
                    TaskStsStr = "Success";
                }
                else
                {
                    TaskStsStr = "Fail";
                }

                OnPropertyChanged("TaskStsStr");
            }
        }


        public override bool Equals(object p)
        {
            if ((p as SocketFileInfo) == null)
                return false;
            return (this.TimeStamp + this.FilePath) == ((p as SocketFileInfo).TimeStamp + (p as SocketFileInfo).FilePath);
        }

        public override int GetHashCode()
        {
            return (this.TimeStamp + this.FilePath).GetHashCode();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private long fileSize = 0;
        public long FileSize
        {
            get
            {
                return fileSize;
            }
            set
            {
                fileSize = value;
                OnPropertyChanged("FileSize");
            }
        }
    }
}
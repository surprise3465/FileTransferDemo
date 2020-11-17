using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using System.Xml;
using Path = System.IO.Path;

namespace WpfSocketDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private string path = AppDomain.CurrentDomain.BaseDirectory;

        ObservableCollection<SocketFileInfo> sendFiles = new ObservableCollection<SocketFileInfo>();
        ObservableCollection<SocketFileInfo> recvFiles = new ObservableCollection<SocketFileInfo>();

        List<string> finishedFiles = new List<string>();

        WebClient webclient = new WebClient();
        private readonly object locker = new object();

        private Thread sendThread = null;

        private string httpUrl = "http://127.0.0.1:8000/";
        //Send Files      

        public MainWindow()
        {
            InitializeComponent();
            InitSocketAndThread();
        }

        private void InitSocketAndThread()
        {
            sendFileDataGrid.ItemsSource = sendFiles;
            recvFileDataGrid.ItemsSource = recvFiles;

            if (sendThread != null && sendThread.IsAlive)
            {
                sendThread.Abort();
                sendThread = null;
            }
            else
            {
                sendThread = new Thread(SendFilesThreadFunc)
                { IsBackground = true, };
                sendThread.Start();
            }
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
                DownloadFiles();
            }
        }

        public class AllFiles
        {
            private List<string> _files = new List<string>();
            public List<string> files { get; set; }
        }

        private List<string> GetFileNames()
        {
            try
            {

                var url = "http://127.0.0.1:8000/getfiles/upload";
                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "Get";
                httpWebRequest.Timeout = 5000;
                //httpWebRequest.Headers.Add("Authorization", currentAuthorization);

                httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/45.0.2454.101 Safari/537.36";
                httpWebRequest.AllowAutoRedirect = true;
                httpWebRequest.KeepAlive = true;//建立持久性连接
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream());
                string responseContent = streamReader.ReadToEnd();

                httpWebResponse.Close();
                streamReader.Close();
                httpWebRequest.Abort();
                httpWebResponse.Close();

                JObject jo = (JObject)JsonConvert.DeserializeObject(responseContent);
                string result = jo["files"].ToString();
                result = result.Replace("[", "");
                result = result.Replace("]", "");
                result = result.Replace("'", "");
                result = result.Replace(" ", "");
                return result.Split(',').ToList();
            }
            catch (Exception ex)
            {
                return new List<string>();
            }
        }

        private void DownloadFiles()
        {
            var fileList = GetFileNames();
            foreach (var file in fileList)
            {
                if (!finishedFiles.Contains(file))
                {
                    DownloadFile(file);
                    finishedFiles.Add(file);
                }
            }

        }

        private bool DownloadFile(string fileName)
        {
            try
            {
                var memStream = new MemoryStream();
                var boundary = "---------------" + DateTime.Now.Ticks.ToString("x");
                // 边界符
                var beginBoundary = Encoding.ASCII.GetBytes("--" + boundary + "\r\n");
                // 最后的结束符
                var endBoundary = Encoding.ASCII.GetBytes("--" + boundary + "--\r\n");

                var url = httpUrl + "download";
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "POST";
                request.ContentType = "multipart/form-data; boundary=" + boundary;
                request.Accept = "*/*";
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                string stringKeyHeader = "--" + boundary + "\r\nContent-Disposition:form-data; name=\"{0}\"\r\n\r\n{1}\r\n";
                var dataByte = Encoding.ASCII.GetBytes(string.Format(stringKeyHeader, "filename", fileName));
                memStream.Write(dataByte, 0, dataByte.Length);//循环写入 参数  

                stringKeyHeader = "--" + boundary + "\r\nContent-Disposition:form-data; name=\"{0}\"\r\n\r\n{1}\r\n";
                var dataByte1 = Encoding.ASCII.GetBytes(string.Format(stringKeyHeader, "path", "upload/"));
                memStream.Write(dataByte1, 0, dataByte1.Length);//循环写入 参数  

                memStream.Write(endBoundary, 0, endBoundary.Length);

                var requestStream = request.GetRequestStream();

                memStream.Position = 0;
                var tempBuffer = new byte[memStream.Length];
                memStream.Read(tempBuffer, 0, tempBuffer.Length);

                requestStream.Write(tempBuffer, 0, tempBuffer.Length);
                //发送请求并获取相应回应数据
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                //直到request.GetResponse()程序才开始向目标网页发送Post请求
                Stream responseStream = response.GetResponseStream();

                //创建本地文件写入流
                Stream stream = new FileStream(path + fileName, FileMode.Create);

                byte[] bArr = new byte[1024];
                int size = responseStream.Read(bArr, 0, (int)bArr.Length);
                while (size > 0)
                {
                    stream.Write(bArr, 0, size);
                    size = responseStream.Read(bArr, 0, (int)bArr.Length);
                }
                stream.Flush();
                stream.Close();
                responseStream.Close();
                SocketFileInfo socketFile = new SocketFileInfo()
                { FilePath = path + fileName, TimeStamp = DateTime.Now.ToString(), FileSize = new FileInfo(path + fileName).Length, TaskSts = TaskStatus.NONE };
                this.Dispatcher.Invoke(new Action(() => { recvFiles.Add(socketFile); }));
                return true;
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.ToString());
                return false;
            }
        }
        private void SendFileToDevice1(SocketFileInfo item)
        {
            try
            {
                WebClient webclient = new WebClient();
                webclient.Encoding = Encoding.UTF8;
                var url = httpUrl + "upload";
                var memStream = new MemoryStream();
                var fileStream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);

                var boundary = "---------------" + DateTime.Now.Ticks.ToString("x");
                var beginBoundary = Encoding.ASCII.GetBytes("--" + boundary + "\r\n");
                var endBoundary = Encoding.ASCII.GetBytes("--" + boundary + "--\r\n");

                webclient.Headers.Add("Content-Type","multipart/form-data; boundary=" + boundary);
                //webclient.UploadFile(url, item.FilePath);
                string filename = item.FilePath.Substring(item.FilePath.LastIndexOf("/") + 1);
                string filePartHeader = "Content-Disposition:form-data; name=\"{0}\"; filename=\"{1}\"\r\n" + "Content-Type:application/octet-stream\r\n\r\n";
                var header = string.Format(filePartHeader, "file", filename);
                var headerbytes = Encoding.ASCII.GetBytes(header);
                memStream.Write(beginBoundary, 0, beginBoundary.Length);
                memStream.Write(headerbytes, 0, headerbytes.Length);
                var base64string = System.Convert.ToBase64String(System.IO.File.ReadAllBytes(item.FilePath));
                var readText = Encoding.ASCII.GetBytes(base64string);
                memStream.Write(readText, 0, readText.Length);
                // 写入最后的结束边界符------------ 3.-------------------------
                memStream.Write(endBoundary, 0, endBoundary.Length);
                var tempBuffer = new byte[memStream.Length];
                memStream.Read(tempBuffer, 0, tempBuffer.Length);
                var bytess = webclient.UploadData(url, tempBuffer);
                //var bytess = webclient.UploadFile(url, item.FilePath);
                string res = Encoding.UTF8.GetString(bytess);
                Console.WriteLine(res);
                item.TaskSts = TaskStatus.SUCCESS;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void SendFileToDevice(SocketFileInfo item)
        {
            try
            {
                var url = httpUrl + "upload";
                var memStream = new MemoryStream();
                var webRequest = (HttpWebRequest)WebRequest.Create(url);

                var boundary = "---------------" + DateTime.Now.Ticks.ToString("x");
                var beginBoundary = Encoding.ASCII.GetBytes("--" + boundary + "\r\n");
                var endBoundary = Encoding.ASCII.GetBytes("--" + boundary + "--\r\n");

                // 设置属性
                webRequest.Method = "POST";
                webRequest.Timeout = 5000;
                webRequest.ContentType = "multipart/form-data; boundary=" + boundary;
                webRequest.Accept = @"*/*";
                webRequest.Headers.Add("Accept-Encoding", "gzip,deflate,br,sdch");

                var fileInfo = new FileInfo(item.FilePath);
                var size = fileInfo.Length;
                var envpath = fileInfo.DirectoryName;
                string newfilename = "new"+fileInfo.Name;
                string newpath = envpath + @"\" + newfilename;

                string filePartHeader = "Content-Disposition:form-data; name=\"{0}\"; filename=\"{1}\"\r\n" + "Content-Type:application/t\r\n\r\n";
                
                var header = string.Format(filePartHeader, "file", newpath);
                var headerbytes = Encoding.ASCII.GetBytes(header);
                memStream.Write(beginBoundary, 0, beginBoundary.Length);
                memStream.Write(headerbytes, 0, headerbytes.Length);

                var base64string  = File.ReadAllText(item.FilePath)+"\r\n";              
                var readText = Encoding.UTF8.GetBytes(base64string);
                memStream.Write(readText, 0, readText.Length);
                // 写入最后的结束边界符------------ 3.-------------------------
                memStream.Write(endBoundary, 0, endBoundary.Length);

                webRequest.ContentLength = memStream.Length;

                var requestStream = webRequest.GetRequestStream();
                memStream.Position = 0;
                var tempBuffer = new byte[memStream.Length];
                memStream.Read(tempBuffer, 0, tempBuffer.Length);
                requestStream.Write(tempBuffer, 0, tempBuffer.Length);

                //响应 ------------------- 4.-----------------------------------
                var httpWebResponse = (HttpWebResponse)webRequest.GetResponse();
                string responseContent;
                using (var httpStreamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.GetEncoding("utf-8")))
                {
                    responseContent = httpStreamReader.ReadToEnd();
                }
                Console.WriteLine(responseContent);
                httpWebResponse.Close();
                webRequest.Abort();
                //fileStream.Close();
                memStream.Close();
                item.TaskSts = TaskStatus.SUCCESS;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private string InitConfigXML(string inputfile, string input)
        {
            var myFs = new FileStream(inputfile, FileMode.Create);
            var mySw = new StreamWriter(myFs);
            mySw.WriteLine("<?xml version=\"1.0\"?>");
            mySw.WriteLine(
                "<Config>\r\n</Config>\r\n");
            mySw.Close();
            myFs.Close();

            XmlDocument doc = new XmlDocument();//新建对象
            doc.Load(inputfile);

            XmlNode top = doc.SelectSingleNode("Config");
            top.InnerText = input;
            doc.Save(inputfile);

            return doc.OuterXml;
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
                errorSts.Text = "";
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
                }
                else
                {
                    sendThread = new Thread(SendFilesThreadFunc);
                    sendThread.Start();
                }
            }
            catch (SocketException ex)
            {
                infoSts.Text = ex.ToString();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            System.Environment.Exit(0);
        }

        private void btnGetFiles_Click(object sender, RoutedEventArgs e)
        {

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
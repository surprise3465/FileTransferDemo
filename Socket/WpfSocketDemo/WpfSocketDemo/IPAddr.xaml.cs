using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfSocketDemo
{
    public enum TaskStatus
    {
        NONE,
        WORKING,
        SUCCESS,
        FAIL,
    };

    /// <summary>
    /// IPAddr.xaml 的交互逻辑
    /// </summary>
    public class SocketInfo
    {
        public string Address { get; set; }

        public string PhysicalAddress { get; set; }
        public string Description { get; set; }
    }

    
    public partial class IPAddr : Window
    {
        public string ipaddr = "";        

        public IPAddr()
        {
            InitializeComponent();
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            List<SocketInfo> socketInfos = new List<SocketInfo>(); 
            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    UnicastIPAddressInformationCollection c = adapter.GetIPProperties().UnicastAddresses;
                    foreach (UnicastIPAddressInformation ipaddr in c)
                    {
                        if (ipaddr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var sockInfo = new SocketInfo();
                            sockInfo.Address = ipaddr.Address.ToString();
                            sockInfo.PhysicalAddress = adapter.GetPhysicalAddress().ToString();
                            sockInfo.Description = adapter.Description;
                            socketInfos.Add(sockInfo);
                        }
                    }
                }
            }
            this.IPAddrDataGrid.ItemsSource = socketInfos;
        }

        private void btnSelect(object sender, RoutedEventArgs e)
        {
            try
            {
                int index = IPAddrDataGrid.SelectedIndex;
                ipaddr = (IPAddrDataGrid.Columns[1].GetCellContent(IPAddrDataGrid.Items[index]) as TextBlock).Text;
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                this.DialogResult = false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Sockets;
using Sharp7;
using NModbus;

namespace Operator_Station_WPF
{

    public partial class MainWindow : Window
    {
        S7Client s7_client;
        ModbusFactory mbus_factory;
        IModbusMaster mbus_client;
        TcpClient mbus_tcpc;
        int client_connected;

        public MainWindow()
        {
            InitializeComponent();

            s7_client = new S7Client();
            mbus_factory = new ModbusFactory();
            
            client_connected = 0;
        }


        void OnConnectButtonClicked(object sender, RoutedEventArgs e)
        {
            string ip_addr = ip_textbox.Text;

            if (client_connected == 0)
            {
                try
                {
                    if (s7_radio.IsChecked == true)
                    {
                        s7_client.ConnectTo(ip_addr, 0, 2);
                        client_connected = 1;
                        conn_status_label.Content = "Соединен с " + ip_addr + " по протоколу Modbus";
                    }
                    else if (modbus_radio.IsChecked == true)
                    {
                        mbus_tcpc = new TcpClient();
                        mbus_client = mbus_factory.CreateMaster(mbus_tcpc);
                        mbus_tcpc.Connect(ip_addr, 502);
                        client_connected = 2;
                        conn_status_label.Content = "Соединен с " + ip_addr + "по протоколу S7 Communication";
                    }
                }
                catch
                {
                    conn_status_label.Content = "Ошибка соединения";
                    return;
                }
                connect_button.Content = "Отсоединить";
            }
            else
            {
                if (client_connected == 1)
                {
                    mbus_tcpc.Close();
                }
                else if (client_connected == 2)
                {
                    s7_client.Disconnect();
                }

                client_connected = 0;
                connect_button.Content = "Соединить";
            }
        }
    }
}

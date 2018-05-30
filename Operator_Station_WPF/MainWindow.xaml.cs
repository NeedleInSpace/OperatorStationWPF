using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Media;
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
        CancellationTokenSource ct_source;
        CancellationToken update_task_ctoken;
        bool auto_mode = true;

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
                ct_source = new CancellationTokenSource();
                update_task_ctoken = ct_source.Token;
                try
                {
                    if (s7_radio.IsChecked == true)
                    {
                        s7_client.ConnectTo(ip_addr, 0, 2);
                        client_connected = 1;
                        conn_status_label.Content = "Соединен с " + ip_addr + " по\nпротоколу S7 Communication";
                        Task.Factory.StartNew(S7Update, update_task_ctoken);
                        
                    }
                    else if (modbus_radio.IsChecked == true)
                    {
                        mbus_tcpc = new TcpClient();
                        mbus_client = mbus_factory.CreateMaster(mbus_tcpc);
                        mbus_tcpc.Connect(ip_addr, 502);
                        client_connected = 2;
                        conn_status_label.Content = "Соединен с " + ip_addr + " по\nпротоколу Modbus";
                        Task.Factory.StartNew(ModbusUpdate, update_task_ctoken);
                    }
                }
                catch
                {
                    conn_status_label.Content = "Ошибка соединения";
                    return;
                }
                autoswitch_button.IsEnabled = true;
                connect_button.Click -= OnConnectButtonClicked;
                connect_button.Click += OnDisconnectButtonClicked;
                connect_button.Content = "Отсоединить";
            }
        }

        public void OnDisconnectButtonClicked(object sender, RoutedEventArgs e)
        {
            if (client_connected == 2)
            {
                ct_source.Cancel();
                client_connected = 0;
                mbus_tcpc.Close();
            }
            else if (client_connected == 1)
            {
                ct_source.Cancel();
                client_connected = 0;
                s7_client.Disconnect();
            }

            autoswitch_button.IsEnabled = false;
            alarm_pressdang.IsEnabled = false;
            alarm_presshigh.IsEnabled = false;
            alarm_watrhigh.IsEnabled = false;
            alarm_watrlow.IsEnabled = false;

            connect_button.Click -= OnDisconnectButtonClicked;
            connect_button.Click += OnConnectButtonClicked;
            connect_button.Content = "Соединить";
            conn_status_label.Content = "Нет соединения";
        }

        void ModbusUpdate()
        {
            float gas_v;
            float pump;
            float steam_v;
            float water_lvl;
            float pressure;
            ushort alrm;
            bool torch;
            while (client_connected == 2)
            {
                Thread.Sleep(500);
                if (update_task_ctoken.IsCancellationRequested)
                {
                    return;
                }
                gas_v = UshortToFloat(mbus_client.ReadHoldingRegisters(0x00, 0, 2));
                pump = UshortToFloat(mbus_client.ReadHoldingRegisters(0x00, 2, 2));
                steam_v = UshortToFloat(mbus_client.ReadHoldingRegisters(0x00, 4, 2));
                water_lvl = UshortToFloat(mbus_client.ReadHoldingRegisters(0x00, 6, 2));
                pressure = UshortToFloat(mbus_client.ReadHoldingRegisters(0x00, 8, 2));
                alrm = mbus_client.ReadHoldingRegisters(0x00, 10, 1)[0];
                torch = mbus_client.ReadCoils(0x00, 5, 1)[0];
                Dispatcher.Invoke(() =>
                {
                    valve2_label.Content = String.Format("КЛ2: {0:F1}%", gas_v * 100);
                    pump_label.Content = String.Format("НАС: {0:F1}%", pump * 100);
                    valve1_label.Content = String.Format("КЛ1: {0:F1}%", steam_v * 100);
                    waterlvl_label.Content = String.Format("{0:F1}%", water_lvl * 100);
                    waterlvl_progressbar.Value = water_lvl;
                    pressure_label.Content = String.Format("{0:F1}%", pressure * 100);
                    pressure_progressbar.Value = pressure;
                    if (torch)
                    {
                        heater_status_ellipse.Fill = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        heater_status_ellipse.Fill = new SolidColorBrush(Colors.Red);
                    }

                    if (alrm == 1) {
                        alarm_presshigh.IsEnabled = true;
                    }
                    else if (alrm == 2)
                    {
                        alarm_pressdang.IsEnabled = true;
                    }
                    else if (alrm == 3)
                    {
                        alarm_watrlow.IsEnabled = true;
                    }
                    else if (alrm == 4)
                    {
                        alarm_watrhigh.IsEnabled = true;
                    }
                    else if (alrm == 0)
                    {
                        alarm_pressdang.IsEnabled = false;
                        alarm_presshigh.IsEnabled = false;
                        alarm_watrhigh.IsEnabled = false;
                        alarm_watrlow.IsEnabled = false;
                    }
                });
                if (auto_mode)
                {

                }
            }
        }

        void S7Update()
        {
            float gas_v;
            float pump;
            float steam_v;
            float water_lvl;
            float pressure;
            ushort alrm;
            bool torch;

            while (client_connected == 1)
            {
                Thread.Sleep(500);
                if (update_task_ctoken.IsCancellationRequested)
                {
                    return;
                }
                byte[] buffer = new byte[24];

                s7_client.DBRead(1, 0, 24, buffer);
                gas_v = S7.GetRealAt(buffer, 0);
                pump = S7.GetRealAt(buffer, 4);
                steam_v = S7.GetRealAt(buffer, 8);
                water_lvl = S7.GetRealAt(buffer, 12);
                pressure = S7.GetRealAt(buffer, 16);
                alrm = S7.GetUIntAt(buffer, 20);
                torch = S7.GetBitAt(buffer, 22, 0);
                Dispatcher.Invoke(() =>
                {
                    valve2_label.Content = String.Format("КЛ2: {0:F1}%", gas_v * 100);
                    pump_label.Content = String.Format("НАС: {0:F1}%", pump * 100);
                    valve1_label.Content = String.Format("КЛ1: {0:F1}%", steam_v * 100);
                    waterlvl_label.Content = String.Format("{0:F1}%", water_lvl * 100);
                    waterlvl_progressbar.Value = water_lvl;
                    pressure_label.Content = String.Format("{0:F1}%", pressure * 100);
                    pressure_progressbar.Value = pressure;
                    if (torch)
                    {
                        heater_status_ellipse.Fill = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        heater_status_ellipse.Fill = new SolidColorBrush(Colors.Red);
                    }
                    if (alrm == 1)
                    {
                        alarm_presshigh.IsEnabled = true;
                    }
                    else if (alrm == 2)
                    {
                        alarm_pressdang.IsEnabled = true;
                    }
                    else if (alrm == 3)
                    {
                        alarm_watrlow.IsEnabled = true;
                    }
                    else if (alrm == 4)
                    {
                        alarm_watrhigh.IsEnabled = true;
                    }
                    else if (alrm == 0)
                    {
                        alarm_pressdang.IsEnabled = false;
                        alarm_presshigh.IsEnabled = false;
                        alarm_watrhigh.IsEnabled = false;
                        alarm_watrlow.IsEnabled = false;
                    }
                });
                if (auto_mode)
                {

                }
            }
        }

        static float UshortToFloat(ushort[] buffer)
        {
            byte[] msb = BitConverter.GetBytes(buffer[1]);
            byte[] lsb = BitConverter.GetBytes(buffer[0]);
            byte[] bytes =
            {
                msb[0], msb[1], lsb[0], lsb[1]
            };
            return BitConverter.ToSingle(bytes, 0);
        }

        public void OnAutoswitchButtonClicked(object sender, RoutedEventArgs e)
        {
            if (auto_mode)
            {
                auto_mode = false;
                autoswitch_button.Content = "РУЧ";
            }
            else
            {
                auto_mode = true;
                autoswitch_button.Content = "АВТО";
            }
        }

    }
}

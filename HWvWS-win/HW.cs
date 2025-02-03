using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Net.Configuration;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace HWvWS_win
{
    class HW
    {
        private string[] _comports = { };
        private static ILog _log;
        private IntPtr _hwnd_handle;
        private IntPtr _notification_handle;
        enum ConnectionType {
            COM = 1,
        };
        struct Connection {
            public ConnectionType _type;
            public SerialPort _serialPort;
            public Connection(SerialPort serialPort)
            {
                this._type = ConnectionType.COM;
                this._serialPort = serialPort;
            }
        };

        private Dictionary<string, Connection> _connections = new Dictionary<string, Connection>();

            /* Copied from: https://stackoverflow.com/questions/16245706/check-for-device-change-add-remove-events/16245901 */

        public const int DbtDeviceArrival = 0x8000; // system detected a new device        
        public const int DbtDeviceRemoveComplete = 0x8004; // device is gone     
        public const int DbtDevNodesChanged = 0x0007; //A device has been added to or removed from the system.

        public const int WmDevicechange = 0x0219; // device change event      
        private const int DbtDevtypDeviceinterface = 5;
        //https://msdn.microsoft.com/en-us/library/aa363431(v=vs.85).aspx
        private const int DEVICE_NOTIFY_SERVICE_HANDLE = 1;
        private const int DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 4;
        private static readonly Guid GuidDevinterfaceUSBDevice = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED"); // USB devices

        public bool update()
        {
            string[] curr_comports = SerialPort.GetPortNames();
            if (!Enumerable.SequenceEqual(curr_comports, _comports))
            {
                _comports = curr_comports;
                _log.Info("Updating comports to: " + string.Join(",", _comports));
                return true;
            }
            return false;
        }
        public HW(ILog log)
        {
            _log = log;
            update();
        }
        public string[] com_baud_list()
        {
            string[] ret = { "50", "75", "110", "134", "150", "200", "300", "600", "1200", "1800", "2400", "4800", "9600", "19200", "28800", "38400", "57600", "76800", "115200", "230400", "460800", "576000", "921600" };
            return ret;
        }

        public string[] com_parity_list()
        {
            return Enum.GetNames(typeof(Parity));
        }

        public string[] com_stopbits_list()
        {
            return Enum.GetNames(typeof(StopBits));
        }

        public string[] com_databits_list()
        {
            string[] ret = { "7", "8" };
            return ret;
        }

        private string open_connection_COM(JObject o)
        {
            string portname = o.GetValue("Port").ToString();
            string s_baud = o.GetValue("Baud").ToString();
            string s_parity = o.GetValue("Parity").ToString();
            string s_databits = o.GetValue("DataBits").ToString();
            string s_stopbits = o.GetValue("StopBits").ToString();
            _log.Debug("portname:" + portname);
            _log.Debug("s_baud:" + s_baud);
            _log.Debug("s_parity:" + s_parity);
            _log.Debug("s_databits:" + s_databits);
            _log.Debug("s_stopbits:" + s_stopbits);
            int baud = Int32.Parse(s_baud);
            _log.Debug("baud:" + baud);
            Parity parity = (Parity)Enum.Parse(typeof(Parity), s_parity);
            _log.Debug("parity:" + parity);
            int databits = Int32.Parse(s_databits);
            _log.Debug("databits:" + databits);
            StopBits stopbits = (StopBits)Enum.Parse(typeof(StopBits), s_stopbits);
            _log.Debug("stopbits:" + stopbits);

            SerialPort port = new SerialPort(portname, baud, parity, databits, stopbits);
            port.Open();
            string id = portname + "." + s_baud + "." + s_parity + "." + s_databits + "." + s_stopbits;
            _log.Info("Opened " + portname + " with id " + id);
            _connections.Add(id, new Connection(port));
            return id;
        }

        public string open_connection(JObject o)
        {
            if (!o.ContainsKey("type"))
                throw new ArgumentException("o.type must be defined", nameof(o));

            ConnectionType type = (ConnectionType)Enum.Parse(typeof(ConnectionType), o.GetValue("type").ToString());
            switch (type) {
                case ConnectionType.COM:
                    return open_connection_COM(o);
            }
            return "Unknown type";
        }

        public void close_connection(JObject o)
        {
            string id = o.GetValue("connection_id").ToString();
            Connection connection = _connections[id];
            ConnectionType type = connection._type;
            switch (type) {
                case ConnectionType.COM:
                    connection._serialPort.Close();
                    break;
            }
            _connections.Remove(id);
            _log.Info("Closed " + id);
        }

        public void connection_tx(JObject o)
        {
            string id = o.GetValue("connection_id").ToString();
            string msg = o.GetValue("msg").ToString();
            _log.Info(id + " TX " + msg);
            Connection connection = _connections[id];
            _log.Debug("connection: " + connection);
            ConnectionType type = connection._type;
            _log.Debug("type: " + type);
            switch (type)
            {
                case ConnectionType.COM:
                    _log.Debug("Write()");
                    connection._serialPort.Write(msg);
                    _log.Debug("~Write()");
                    break;
            }
        }

        public void start(IntPtr handle)
        {
            _hwnd_handle = handle;
            var dbi = new DevBroadcastDeviceinterface
            {
                DeviceType = DbtDevtypDeviceinterface,
                Reserved = 0,
                ClassGuid = GuidDevinterfaceUSBDevice,
                Name = 0
            };

            dbi.Size = Marshal.SizeOf(dbi);
            IntPtr buffer = Marshal.AllocHGlobal(dbi.Size);
            Marshal.StructureToPtr(dbi, buffer, true);

            _notification_handle = RegisterDeviceNotification(_hwnd_handle, buffer, DEVICE_NOTIFY_SERVICE_HANDLE | DEVICE_NOTIFY_ALL_INTERFACE_CLASSES);
        }

        public void stop()
        {
            UnregisterDeviceNotification(_notification_handle);
        }
        public string[] com_ports()
        {
            return _comports;
        }

        public string[] connection_ids()
        {
            return _connections.Keys.ToArray();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr notificationFilter, int flags);
        [DllImport("user32.dll")]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DevBroadcastDeviceinterface
        {
            internal int Size;
            internal int DeviceType;
            internal int Reserved;
            internal Guid ClassGuid;
            internal short Name;
        }
    }
}

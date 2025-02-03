using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using HWvWS_win.Properties;
using System.Text;
using log4net;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

namespace HWvWS_win
{
    // Copied from https://www.c-sharpcorner.com/article/websocket-in-net/
    class WebSocketServer
    {
        private static ILog _log;
        private static HW _hw;
        private string[] _prefixes;
        private bool _is_listening;
        private static HttpListener _listener;
        private static List<WebSocket> _sockets;
        public WebSocketServer(ILog log, string[] prefixes, HW hw)
        {
            _log = log;
            _prefixes = prefixes;
            _is_listening = false;
            _hw = hw;
        }

        public void start()
        {
            if (_is_listening)
            {
                _log.Debug("Web socket server start attempted, but it is already started.");
                return;
            }
            if (!HttpListener.IsSupported)
            {
                _log.Error("Web socket server start failed.  Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }
            if (_prefixes == null || _prefixes.Length == 0)
            {
                _log.Error("Web socket server start failed: prefixes invalid.");
                return;
            }
            // Create a listener.
            _listener = new HttpListener();
            // Add the prefixes.
            foreach (string s in _prefixes)
            {
                _log.Debug("_listener.Prefixes.Add(" + s + ")");
                _listener.Prefixes.Add(s);
            }
            _listener.Start();
            _log.Info("Webserver Listening...");
            _is_listening = true;
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), null);
            _sockets = new List<WebSocket>();
        }
        public void shutdown()
        {
            _listener.Stop();
            _is_listening = false;
            _log.Info("Web socket server Stopped...");

        }

        private void ListenerCallback(IAsyncResult result)
        {
            HttpListenerContext context = _listener.EndGetContext(result);
            _log.Info("Web socket server request from " + context.Request.RemoteEndPoint.ToString());
            if (!context.Request.IsWebSocketRequest)
            {
                _log.Info("Web socket server received request that isn't web socket.");
                context.Response.StatusCode = 400;
                context.Response.Close();
                _listener.BeginGetContext(new AsyncCallback(ListenerCallback), null);
                return;
            }
            _log.Info("Web socket server processing web socket request.");
            ProcessWebSocketRequest(context);
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), null);
        }
        private async Task SendOpenDetails(WebSocket socket, string connection_id)
        {
            JObject o = new JObject();
            o["id"] = "open";
            o["version"] = "1.0";
            o["connection_id"] = connection_id;
            string msg = o.ToString();
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        private async Task SendCloseDetails(WebSocket socket, string connection_id)
        {
            JObject o = new JObject();
            o["id"] = "close";
            o["version"] = "1.0";
            o["connection_id"] = connection_id;
            string msg = o.ToString();
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        private async Task SendConnectionDetails(WebSocket socket)
        {
            /*
             * {
             *   "id": "hardware",
             *   "types": [ "COM" ],
             *   "COM-parameters": [ "Port", "Baud", "Parity", "DataBits", "StopBits" ],
             *   "COM-Port": [ "COM6" ],
             *   "COM-Baud": [ "50","75","110","134","150","200","300","600","1200","1800","2400","4800","9600","19200","28800","38400","57600","76800","115200","230400","460800","576000","921600" ]
             *   "COM-Parity": [ "Mark", "None", "Odd", "Even", "Space" ],
             *   "COM-DataBits": [ "7", "8" ],
             *   "COM-StopBits": [ "One", "OnePointFive", "Two", "None" ],
             *   "USB-HID-parameters" = [ "Device" ],
             *   "USB-HID-Device" = [ "\nVendorID: XXX\nProductID: XXX\nDescription:XXXX" ]
             *   }
             */
            string[] COM_parameters = { "Port", "Baud", "Parity", "DataBits", "StopBits" };
            JObject o = new JObject();
            o["id"] = "hardware";
            o["version"] = "1.0";
            JArray types = new JArray();
            if (_hw.com_ports().Length > 0)
            {
                types.Add("COM");
            }
            o["types"] = types;
            o["COM-parameters"] = new JArray(COM_parameters);
            o["COM-Port"] = new JArray(_hw.com_ports());
            o["COM-Baud"] = new JArray(_hw.com_baud_list());
            o["COM-Parity"] = new JArray(_hw.com_parity_list());
            o["COM-DataBits"] = new JArray(_hw.com_databits_list());
            o["COM-StopBits"] = new JArray(_hw.com_stopbits_list());
            
            o["connections"] = new JArray(_hw.connection_ids());
            string msg = o.ToString();
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void SendAllSockets(Func<WebSocket, Task> action, string desc)
        {
            if (!_is_listening)
                return;

            _log.Info(desc + " to " + _sockets.Count + " websockets");
            foreach (WebSocket socket in _sockets)
            {
                action(socket);
            }
            _log.Info("Finished " + desc);
        }

        public void SendConnectionDetails()
        {
            SendAllSockets(SendConnectionDetails, "SendConnectionDetails");
        }

        private async Task ProcessWebSocketRequest(HttpListenerContext context)
        {
            HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
            WebSocket socket = webSocketContext.WebSocket;

            _sockets.Add(socket);

            SendConnectionDetails();

            // Handle incoming messages
            byte[] buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string receivedMessage = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _log.Info($"Received message: {receivedMessage}");
                    try
                    {
                        JObject o = JObject.Parse(receivedMessage);
                        string msg_id = o.GetValue("id").ToString();
                        if (msg_id == "open")
                        {
                            string id = _hw.open_connection(o);
                            SendConnectionDetails();
                            await SendOpenDetails(socket, id);
                        }
                        else if (msg_id == "close")
                        {
                            string id = o.GetValue("connection_id").ToString();
                            _hw.close_connection(o);
                            SendConnectionDetails();
                            await SendCloseDetails(socket, id);
                        }
                        else if (msg_id == "send")
                        {
                            _hw.connection_tx(o);
                        }
                        else
                        {
                            throw new ArgumentException("Unknown msg id", nameof(o));
                        }
                    } catch (Exception ex)
                    {
                        // Echo back the received message
                        _log.Warn("Unhandled msg received: " + receivedMessage);
                        await socket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            }

            _sockets.Remove(socket);
        }
    }
}

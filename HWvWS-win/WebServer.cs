using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using log4net;
using System.Threading;
using System.Data;
using System.Runtime.Versioning;
using HWvWS_win.Properties;
using System.Web.Management;

namespace HWvWS_win
{
    public class WebServer
    {
        private static ILog _log;
        private string[] _prefixes;
        private bool _is_listening;
        private static HttpListener _listener;
        public WebServer(ILog log, string[] prefixes)
        {
            _log = log;
            _prefixes = prefixes;
            _is_listening = false;
        }

        public void start()
        {
            if (_is_listening)
            {
                _log.Debug("Webserver start attempted, but it is already started.");
                return;
            }
            if (!HttpListener.IsSupported)
            {
                _log.Error("Webserver start failed.  Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }
            if (_prefixes == null || _prefixes.Length == 0)
            {
                _log.Error("Webserver start failed: prefixes invalid.");
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
        }

        public void shutdown()
        {
            _listener.Stop();
            _is_listening = false;
            _log.Info("Webserver Stopped...");

        }

        // Meat copied from:
        // https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=net-9.0
        // This example requires the System and System.Net namespaces.

        private static void ListenerCallback(IAsyncResult result)
        {
            HttpListenerContext context = _listener.EndGetContext(result);
            _log.Info("Webserver request from " + context.Request.RemoteEndPoint.ToString());
            HttpListenerRequest request = context.Request;
            // Obtain a response object.
            HttpListenerResponse response = context.Response;
            // Construct a response.
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(Resources.index_html);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
            // Restart listening handling.
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), null);
        }
    }
}


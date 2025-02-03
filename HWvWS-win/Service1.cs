using System;
using System.IO.Ports;
using System.ServiceProcess;
using System.Timers;
using System.Runtime.InteropServices;
using System.Configuration;

namespace HWvWS_win
{
    public partial class Service1 : ServiceBase
    {
        Timer timer = new Timer();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static string[] webserver_prefixes = { ConfigurationManager.AppSettings["host"] };
        private static string[] websocketserver_prefixes = { ConfigurationManager.AppSettings["host"] + "ws/" };
        //        private static string[] websocketserver_prefixes = { "http://+:8080/ws/" };
        private static HW hw = new HW(log);
        private WebServer webServer = new WebServer(log, prefixes: webserver_prefixes);
        private WebSocketServer webSocketServer = new WebSocketServer(log, prefixes: websocketserver_prefixes, hw);

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            log4net.Util.LogLog.InternalDebugging = true;
            log.Info("Service is started at " + DateTime.Now);
            webServer.start();
            webSocketServer.start();
            hw.start(ServiceHandle);

            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            timer.Interval = 5000;
            timer.Enabled = true;
        }

        protected override void OnStop()
        {
            hw.stop();
            webSocketServer.shutdown();
            webServer.shutdown();
            log.Info("Service is stopped at " + DateTime.Now);
        }

        private const int SERVICE_CONTROL_DEVICEEVENT = 11;
        protected override void OnCustomCommand(int command)
        {
            if (command == SERVICE_CONTROL_DEVICEEVENT)
            {
                if (hw.update())
                {
                    webSocketServer.SendConnectionDetails();
                }
            }
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            log.Info("Heartbeat");
        }
    }
}
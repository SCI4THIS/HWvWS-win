Hardware via Websockets, specifically for windows (C#).

In order to test the service follow the tutorial listed in the c-sharpcorner URL.
Notably, run a command as an administrator and

cd C:\Windows\Microsoft.NET\Framework\v4.0.30319
InstallUtil.exe <PATH>\HWvWS.exe

Rebuilding in MSVC will update the service that can be started and stopped from
services.msc


Development notes:

Created service app
  with service tutorial code from 
    https://www.c-sharpcorner.com/article/create-windows-services-in-c-sharp/

Added log4net
  PM> Install-Package log4net
  loosely following the following tutorial, replaced the simple WriteToFile log.
    https://stackify.com/log4net-guide-dotnet-logging/
  this was a little tricky, because to finally get it working I had to add the
  XMLConfigurator to an assembly: command as described here
    https://stackoverflow.com/a/14682889
  Several attempts to do this with service OnStart calling XMLConfigurator did not work.

Added WebServer.cs 
  with the simple listener web-server example from
    https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=net-9.0
  combined with the article on threading from here
    https://www.c-sharpcorner.com/article/Threads-in-CSharp/
  created a WebServer class that runs in a thread when start() is called.
  Updated to using async listener.
    https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener.begingetcontext?view=net-9.0
  index.html created from a resource file.

* Copied index.html from nuklear-wasm project. 


Added WebSocketServer.cs
  with the web socket server example here:
    https://www.c-sharpcorner.com/article/websocket-in-net/
  await in C# does NOT work the same as Javascript.
  Execution forks, pushing a closure that execution will resume after the task finishes, and
  also returns immediately to the caller.

Created HW.cs
  Enumerate serial ports
    https://stackoverflow.com/questions/5445847/list-available-com-ports
  RegisterDeviceNotification bit from
    https://stackoverflow.com/questions/16245706/check-for-device-change-add-remove-events/16245901
  PM> Install-Package Newtonsoft.Json
  Installed Newtonsoft.Json for the JSON creation.
  


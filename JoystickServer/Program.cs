using JoystickServer;
using System.Net;
using System.Net.Sockets;

namespace JoystickServer
{
    /// <summary>
    /// This is the main program
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Starts the gamepad server
        /// </summary>
        /// <param name="verbose">When true, additional information is logged to the console.</param>
        /// <param name="timeout">Specifies the allowed time between connections from the same device when interrupted (ms).</param>
        /// <param name="port">The port to open the server on.</param>
        /// <param name="hotReload">Reload</param>
        /// <returns></returns>
        public static async Task Main(bool verbose = false, int timeout = 5000, int port = 3000, DirectoryInfo? hotReload = null)
        {
            WebServer server = new(verbose, timeout, port);
            if(hotReload != null)
                server.StartHotReload(hotReload);

            ClientDriver.Initialize();

            Dictionary<Guid, ClientDriver> clients = [];

            WebServer.JoystickClient.ClientAdded += (s, e) =>
            {
                Console.WriteLine("[Client]: " + e.Id + " connected");
                if (e.Client != null)
                    clients.Add(e.Id, new ClientDriver(e.Client, verbose));
            };

            WebServer.JoystickClient.ClientRemoved += (s, e) =>
            {
                Console.WriteLine("[Client]: " + e.Id + " disconnected");
                if (clients.Remove(e.Id, out ClientDriver? client))
                    client.Dispose();
            };

            Console.WriteLine("[Server]: Starting");
            server.Start();

            var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine("[Server] listening on http://" + ip.ToString() + ":" + server.Port + "/app/index.html");
                }
            }

            Console.WriteLine("[Server]: Press any key to exit");
            Console.ReadLine();

            Console.WriteLine("[SERVER]: Stopping");
            server.StopHotReload();
            server.Stop();

            foreach (var client in clients.Values)
            {
                client.Dispose();
            }

            clients.Clear();

            ClientDriver.Destroy();
        }
    }
}
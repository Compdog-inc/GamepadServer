using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace JoystickServer
{
    internal class WebServer
    {
        public string Address => ws.Address;
        public int Port => ws.Port;

        public class JoystickClientEventArgs : EventArgs
        {
            public Guid Id { get; }
            public JoystickClient? Client { get; }

            internal JoystickClientEventArgs(Guid id, JoystickClient? client)
            {
                Id = id;
                Client = client;
            }
        }

        public class JoystickClient
        {
            public static Dictionary<Guid, JoystickClient> Clients = new Dictionary<Guid, JoystickClient>();

            public Guid Id { get; }

            public static event EventHandler<JoystickClientEventArgs>? ClientAdded;
            public static event EventHandler<JoystickClientEventArgs>? ClientRemoved;

            private int connectionTimeout;

            private JsonNode? data;
            private bool dirty = false;
            private CancellationTokenSource? dirtySource;

            public short GetLeftX()
            {
                return data?["lx"]?.GetValue<short>() ?? 0;
            }

            public short GetLeftY()
            {
                return data?["ly"]?.GetValue<short>() ?? 0;
            }

            public short GetRightX()
            {
                return data?["rx"]?.GetValue<short>() ?? 0;
            }

            public short GetRightY()
            {
                return data?["ry"]?.GetValue<short>() ?? 0;
            }

            public byte GetTriggerLeft()
            {
                return data?["tl"]?.GetValue<byte>() ?? 0;
            }

            public byte GetTriggerRight()
            {
                return data?["tr"]?.GetValue<byte>() ?? 0;
            }

            public bool GetButtonY()
            {
                return data?["by"]?.GetValue<bool>() ?? false;
            }

            public bool GetButtonB()
            {
                return data?["bb"]?.GetValue<bool>() ?? false;
            }

            public bool GetButtonA()
            {
                return data?["ba"]?.GetValue<bool>() ?? false;
            }

            public bool GetButtonX()
            {
                return data?["bx"]?.GetValue<bool>() ?? false;
            }

            internal JoystickClient(Guid id, int connectionTimeout)
            {
                this.connectionTimeout = connectionTimeout;
                Id = id;
                lock (Clients)
                {
                    Clients.Add(id, this);
                    ClientAdded?.Invoke(this, new JoystickClientEventArgs(id, this));
                }
            }

            internal void SetData(JsonNode? data)
            {
                this.data = data;
            }

            internal void ConnectInstance()
            {
                if (dirty)
                {
                    dirty = false;
                    dirtySource?.Cancel();
                }
            }

            internal void DisconnectInstance()
            {
                if (!dirty)
                {
                    dirty = true;
                    dirtySource = new CancellationTokenSource();
                    new Task(async () =>
                    {
                        try
                        {
                            await Task.Delay(connectionTimeout, dirtySource.Token);
                        }
                        catch { return; }
                        if (!dirtySource.Token.IsCancellationRequested)
                        {
                            try
                            {
                                lock (Clients)
                                {
                                    Clients.Remove(Id);
                                    ClientRemoved?.Invoke(this, new JoystickClientEventArgs(Id, null));
                                }
                            }
                            catch { return; }
                        }
                    }, dirtySource.Token).Start();
                }
            }
        }

        class JoystickWsSession(WsServer server, bool verbose, int connectionTimeout) : WsSession(server)
        {
            public JoystickClient? Client { get; private set; }

            public override void OnWsConnected(HttpRequest request)
            {
                if (verbose)
                    Console.WriteLine($"[Socket]: {Id} connected");
            }

            public override void OnWsDisconnected()
            {
                if (verbose)
                    Console.WriteLine($"[Socket]: {Id} disconnected");

                if (Client != null)
                {
                    Client.DisconnectInstance();
                    Client = null;
                }
            }

            public override void OnWsReceived(byte[] buffer, long offset, long size)
            {
                string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

                // Detect packet id
                if (message.StartsWith("CID") && message.Length == 39)
                {
                    if (Guid.TryParse(message[3..], out Guid uuid))
                    {
                        if (verbose)
                        {
                            Console.WriteLine($"[Socket]: Received CID '{uuid}'");
                            if (Client != null)
                                Console.WriteLine("[Socket]: Internal state already connected! Marking previous CID as dirty.");
                        }

                        Client?.DisconnectInstance();

                        if (JoystickClient.Clients.TryGetValue(uuid, out JoystickClient? value))
                        {
                            if (verbose)
                                Console.WriteLine("[Socket]: Client already axists, adding instance.");

                            Client = value;
                            Client.ConnectInstance();
                        }
                        else
                        {
                            if (verbose)
                                Console.WriteLine("[Socket]: Creating new client");
                            Client = new JoystickClient(uuid, connectionTimeout);
                        }
                        return;
                    }
                    else if (verbose)
                    {
                        Console.WriteLine($"[Socket]: Invalid CID received '{message[3..]}'");
                    }
                }

                // Parse json data
                if (Client != null)
                {
                    var data = JsonNode.Parse(message);
                    Client.SetData(data);
                }
            }

            protected override void OnError(SocketError error)
            {
                Console.WriteLine($"[Socket]: error {error}");
            }
        }

        class JoystickWsServer(IPAddress address, int port, bool verbose, int connectionTimeout) : WsServer(address, port)
        {
            protected override TcpSession CreateSession() { return new JoystickWsSession(this, verbose, connectionTimeout); }

            protected override void OnError(SocketError error)
            {
                Console.WriteLine($"[HTTP]: error {error}");
            }
        }

        FileSystemWatcher? watcher;
        JoystickWsServer ws;
        bool verbose;

        const string staticRoot = "app";

        public WebServer(bool verbose, int connectionTimeout, int port)
        {
            this.verbose = verbose;
            ws = new JoystickWsServer(IPAddress.Any, port, verbose, connectionTimeout);
            ws.AddStaticContent(staticRoot, "/app");
        }

        public void Start()
        {
            try
            {
                ws.Start();
            }
            catch
            {
                Console.WriteLine("[Server]: Error Port already in use!");
                Console.WriteLine("[Server]: Press any key to exit");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        public void Stop()
        {
            ws.DisconnectAll();
            ws.Stop();
            ws.Dispose();
        }

        public void StartHotReload(DirectoryInfo source)
        {
            if (verbose)
                Console.WriteLine("[Server]: Initializing file system watcher");

            watcher = new FileSystemWatcher(source.FullName);
            watcher.IncludeSubdirectories = true;

            watcher.Changed += hotReloadEventHandle;
            watcher.Created += hotReloadEventHandle;
            watcher.Deleted += hotReloadEventHandle;
            watcher.Renamed += hotReloadEventHandle;

            watcher.EnableRaisingEvents = true;
        }

        public void StopHotReload()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }

        private void hotReloadEventHandle(object sender, FileSystemEventArgs e)
        {
            if (watcher == null)
                return;

            string path = Path.GetRelativePath(watcher.Path, e.FullPath);
            if (verbose)
                Console.WriteLine($"[Server]: detected change at '{path}'");

            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    if (File.Exists(e.FullPath))
                        File.Copy(e.FullPath, Path.Join(staticRoot, path), true);
                    break;
                case WatcherChangeTypes.Deleted:
                    File.Delete(Path.Join(staticRoot, path));
                    break;
                case WatcherChangeTypes.Changed:
                    if (File.Exists(e.FullPath))
                        File.Copy(e.FullPath, Path.Join(staticRoot, path), true);
                    break;
            }
        }
    }
}

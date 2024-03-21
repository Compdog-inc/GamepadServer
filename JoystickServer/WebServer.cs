using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace JoystickServer
{
    internal class WebServer
    {
        public static bool Verbose = true;
        public const int ConnectionTimeout = 5000;

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

            internal JoystickClient(Guid id)
            {
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
                            await Task.Delay(ConnectionTimeout, dirtySource.Token);
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
                            }catch { return; }
                        }
                    }, dirtySource.Token).Start();
                }
            }
        }

        class JoystickWsSession(WsServer server) : WsSession(server)
        {
            public JoystickClient? Client { get; private set; }

            public override void OnWsConnected(HttpRequest request)
            {
                if(Verbose)
                    Console.WriteLine($"[Socket]: {Id} connected");
            }

            public override void OnWsDisconnected()
            {
                if(Verbose)
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
                        Client?.DisconnectInstance();

                        if (JoystickClient.Clients.TryGetValue(uuid, out JoystickClient? value))
                        {
                            Client = value;
                            Client.ConnectInstance();
                        }
                        else
                        {
                            Client = new JoystickClient(uuid);
                        }
                        return;
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

        class JoystickWsServer : WsServer
        {
            public JoystickWsServer(IPAddress address, int port) : base(address, port)
            {
            }

            protected override TcpSession CreateSession() { return new JoystickWsSession(this); }

            protected override void OnError(SocketError error)
            {
                Console.WriteLine($"[HTTP]: error {error}");
            }
        }

        JoystickWsServer ws;

        public WebServer()
        {
            ws = new JoystickWsServer(IPAddress.Any, 3000);
            ws.AddStaticContent("app", "/app");
        }

        public void Start()
        {
            ws.Start();
        }

        public void Stop()
        {
            ws.DisconnectAll();
            ws.Stop();
            ws.Dispose();
        }
    }
}

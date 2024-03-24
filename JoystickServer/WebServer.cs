using NetCoreServer;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;

namespace JoystickServer
{
    internal class WebServer
    {
        [Serializable]
        public struct ServerConfig(bool verbose, int connectionTimeout, int port, int? hotReload)
        {
            public int? HotReload = hotReload;

            public int Port = port;

            [NonSerialized]
            [JsonIgnore]
            public bool Verbose = verbose;

            [NonSerialized]
            [JsonIgnore]
            public int ConnectionTimeout = connectionTimeout;

            public readonly string ToJson()
            {
                var serializer = JsonSerializer.Create();

                using var textWriter = new StringWriter();
                using var writer = new JsonTextWriter(textWriter)
                {
                    Formatting = Formatting.None
                };

                serializer.Serialize(writer, this);

                return textWriter.ToString();
            }
        }

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

            private ServerConfig config;

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

            internal JoystickClient(Guid id, ServerConfig config)
            {
                this.config = config;
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
                            await Task.Delay(config.ConnectionTimeout, dirtySource.Token);
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

        class HotreloadWssSession(HotreloadWssServer server, ServerConfig config) : WssSession(server)
        {
            public override void OnWsConnected(HttpRequest request)
            {
                if (config.Verbose)
                    Console.WriteLine($"[Hotreload Socket]: {Id} connected");
            }

            public override void OnWsDisconnected()
            {
                if (config.Verbose)
                    Console.WriteLine($"[Hotreload Socket]: {Id} disconnected");
            }

            protected override void OnError(SocketError error)
            {
                Console.WriteLine($"[Hotreload Socket]: error {error}");
            }
        }

        class JoystickWssSession(JoystickWssServer server, ServerConfig config) : WssSession(server)
        {
            public JoystickClient? Client { get; private set; }

            public override void OnWsConnected(HttpRequest request)
            {
                if (config.Verbose)
                    Console.WriteLine($"[Socket]: {Id} connected");
            }

            public override void OnWsDisconnected()
            {
                if (config.Verbose)
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
                        if (config.Verbose)
                        {
                            Console.WriteLine($"[Socket]: Received CID '{uuid}'");
                            if (Client != null)
                                Console.WriteLine("[Socket]: Internal state already connected! Marking previous CID as dirty.");
                        }

                        Client?.DisconnectInstance();

                        if (JoystickClient.Clients.TryGetValue(uuid, out JoystickClient? value))
                        {
                            if (config.Verbose)
                                Console.WriteLine("[Socket]: Client already axists, adding instance.");

                            Client = value;
                            Client.ConnectInstance();
                        }
                        else
                        {
                            if (config.Verbose)
                                Console.WriteLine("[Socket]: Creating new client");
                            Client = new JoystickClient(uuid, config);
                        }
                        return;
                    }
                    else if (config.Verbose)
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

            protected override void OnReceivedRequest(HttpRequest request)
            {
                if (request.Method == "GET")
                {
                    int num = request.Url.IndexOf('?');
                    string path = (num < 0) ? request.Url : request.Url[..num];
                    switch (path.ToLowerInvariant())
                    {
                        case "/app/api/config":
                            SendResponseAsync(
                                Response.MakeGetResponse(server.Config.ToJson(), "application/json")
                            );
                            return;
                    }
                }

                base.OnReceivedRequest(request);
            }

            protected override void OnError(SocketError error)
            {
                Console.WriteLine($"[Socket]: error {error}");
            }
        }

        class HotreloadWssServer(SslContext context, IPAddress address, ServerConfig config) : WssServer(context, address, config.HotReload ?? 0)
        {
            private static readonly byte[] STATE_CMD_FULL_RELOAD = [0x69, 0x42, 0x13, 0x37];

            public ServerConfig Config { get => config; }

            protected override SslSession CreateSession() { return new HotreloadWssSession(this, config); }

            public void BroadcastChange(string urlPath)
            {
                MulticastText(urlPath);
            }

            public void BroadcastFullReload()
            {
                MulticastBinary(STATE_CMD_FULL_RELOAD);
            }

            protected override void OnError(SocketError error)
            {
                Console.WriteLine($"[HOTRELOAD]: HTTP error {error}");
            }
        }

        class JoystickWssServer(SslContext context, IPAddress address, ServerConfig config) : WssServer(context, address, config.Port)
        {
            public ServerConfig Config { get => config; }

            protected override SslSession CreateSession() { return new JoystickWssSession(this, config); }

            protected override void OnError(SocketError error)
            {
                Console.WriteLine($"[HTTP]: error {error}");
            }
        }

        FileSystemWatcher? watcher;
        JoystickWssServer ws;
        HotreloadWssServer? hotreloadWs;
        ServerConfig config;
        Task? deepCopyTask = null;
        Queue<FileSystemEventArgs> fileSystemUpdates = new Queue<FileSystemEventArgs>();

        SslContext context;

        const string staticRoot = "app";

        public WebServer(ServerConfig config)
        {
            this.config = config;
            context = new SslContext(SslProtocols.Tls13, new X509Certificate2(
                X509Certificate2.CreateFromPemFile(
                    "certs/joystick.local.crt",
                    "certs/joystick.local.key"
                    ).Export(X509ContentType.Pfx))
                );

            ws = new JoystickWssServer(context, IPAddress.Any, config);
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
            if (config.Verbose)
                Console.WriteLine("[Server]: Initializing file system watcher");

            watcher = new FileSystemWatcher(source.FullName);
            watcher.IncludeSubdirectories = true;

            Internal_DeepCopy();

            watcher.Changed += hotReloadEventHandle;
            watcher.Created += hotReloadEventHandle;
            watcher.Deleted += hotReloadEventHandle;
            watcher.Renamed += hotReloadEventHandle;

            watcher.EnableRaisingEvents = true;

            hotreloadWs = new HotreloadWssServer(context, IPAddress.Any, config);
            try
            {
                hotreloadWs.Start();
            }
            catch
            {
                Console.WriteLine("[Server]: Error Hot reload port already in use!");
                hotreloadWs.Dispose();
                hotreloadWs = null;
            }
        }

        public void StopHotReload()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }

            if(hotreloadWs != null)
            {
                hotreloadWs.DisconnectAll();
                hotreloadWs.Stop();
                hotreloadWs.Dispose();
                hotreloadWs = null;
            }
        }

        private void Internal_DeepCopy()
        {
            if (watcher == null)
                return;

            Directory.Delete(staticRoot, true);
            Directory.CreateDirectory(staticRoot);

            string root = watcher.Path;
            var source = new DirectoryInfo(root);
            Stack<(DirectoryInfo, IEnumerator<DirectoryInfo>)> dirTree = new();
            dirTree.Push((source, source.EnumerateDirectories().GetEnumerator()));

            do
            {
                // Get current folder
                (DirectoryInfo dir, IEnumerator<DirectoryInfo> dirs) = dirTree.Peek();

                if (dirs.MoveNext())
                {
                    // if it has subfolders, copy them first by pushing to the stack
                    dirTree.Push((dirs.Current, dirs.Current.EnumerateDirectories().GetEnumerator()));
                    // and create the subfolder
                    Directory.CreateDirectory(Path.Join(staticRoot, Path.GetRelativePath(root, dirs.Current.FullName)));
                }
                else
                {
                    // Copy all files in folder
                    foreach (var file in dir.EnumerateFiles())
                    {
                        file.CopyTo(Path.Join(staticRoot, Path.GetRelativePath(root, file.FullName)), true);
                    }

                    dirTree.Pop(); // nothing left in this folder
                }
            } while (dirTree.Count > 0);

            hotreloadWs?.BroadcastFullReload();
        }

        private void hotReloadEventHandle(object sender, FileSystemEventArgs e)
        {
            if (deepCopyTask == null || deepCopyTask.Status != TaskStatus.Running)
            {
                foreach (var upt in fileSystemUpdates)
                {
                    processFileSystemUpdate(upt);
                }

                processFileSystemUpdate(e);
            }
            else
            {
                fileSystemUpdates.Enqueue(e);
            }
        }

        private void processFileSystemUpdate(FileSystemEventArgs e)
        {
            if (watcher == null)
                return;

            string path = Path.GetRelativePath(watcher.Path, e.FullPath);
            if (config.Verbose)
                Console.WriteLine($"[Server]: detected change at '{path}'");

            try
            {
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
            catch
            {
                if (deepCopyTask == null || deepCopyTask.Status != TaskStatus.Running)
                {
                    if (config.Verbose)
                        Console.WriteLine("[Server]: unexpected error while mirroring file system. Performing deep copy.");
                    deepCopyTask = new Task(async () =>
                    {
                        await Task.Delay(10);
                        Internal_DeepCopy();
                    });
                    deepCopyTask.Start();
                }
            }

            if (e.ChangeType != WatcherChangeTypes.Deleted)
            {
                hotreloadWs?.BroadcastChange("/app/" + path);
            }
        }
    }
}

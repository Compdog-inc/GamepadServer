using JoystickServer;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using System.Net;
using System.Net.Sockets;

WebServer server = new();

ClientDriver.Initialize();

Dictionary<Guid, ClientDriver> clients = new Dictionary<Guid, ClientDriver>();

WebServer.JoystickClient.ClientAdded += (s, e) =>
{
    Console.WriteLine("[Client]: " + e.Id + " connected");
    if(e.Client != null)
        clients.Add(e.Id, new ClientDriver(e.Client));
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
        Console.WriteLine("[Server] listening on http://" + ip.ToString()+":"+server.Port+"/app/index.html");
    }
}

Console.WriteLine("[Server]: Press any key to exit");
Console.ReadLine();

Console.WriteLine("[SERVER]: Stopping");
server.Stop();

foreach(var client in clients.Values)
{
    client.Dispose();
}

clients.Clear();

ClientDriver.Destroy();

class ClientDriver : IDisposable
{
    private static ViGEmClient? driver;

    public static void Initialize()
    {
        driver = new();
    }

    public static void Destroy()
    {
        driver?.Dispose();
    }

    private readonly IXbox360Controller controller;
    private readonly Task controllerTask;
    private readonly CancellationTokenSource tokenSource;

    public ClientDriver(WebServer.JoystickClient client)
    {
        if(driver == null)
            throw new ArgumentNullException("ClientDriver.driver");

        tokenSource = new CancellationTokenSource();

        controller = driver.CreateXbox360Controller();

        if(WebServer.Verbose)
            Console.WriteLine("[Client]: Connecting virtual controller");

        controller.Connect();

        controllerTask = new Task(async () =>
        {
            while (!tokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    controller.SetAxisValue(0, client.GetLeftX());
                    controller.SetAxisValue(1, client.GetLeftY());
                    controller.SetAxisValue(2, client.GetRightX());
                    controller.SetAxisValue(3, client.GetRightY());
                    controller.SetSliderValue(0, client.GetTriggerLeft());
                    controller.SetSliderValue(1, client.GetTriggerRight());

                    controller.SubmitReport();
                    await Task.Delay(1, tokenSource.Token);
                }
                catch { return; }
            }
        }, tokenSource.Token);

        controllerTask.Start();
    }

    public void Dispose()
    {
        tokenSource.Cancel();

        if (!controllerTask.Wait(1000))
            Console.WriteLine("[Client]: Controller feedback time out!");

        if (WebServer.Verbose)
            Console.WriteLine("[Client]: Disconnecting virtual controller");

        controller.Disconnect();
    }
}
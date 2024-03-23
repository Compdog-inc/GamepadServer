using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client;

namespace JoystickServer
{
    internal class ClientDriver : IDisposable
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
        private readonly bool verbose;

        public ClientDriver(WebServer.JoystickClient client, bool verbose)
        {
            this.verbose = verbose;

            if (driver == null)
                throw new ArgumentNullException("ClientDriver.driver");

            tokenSource = new CancellationTokenSource();

            controller = driver.CreateXbox360Controller();

            if (verbose)
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

                        controller.SetButtonState(Xbox360Button.Y, client.GetButtonY());
                        controller.SetButtonState(Xbox360Button.B, client.GetButtonB());
                        controller.SetButtonState(Xbox360Button.A, client.GetButtonA());
                        controller.SetButtonState(Xbox360Button.X, client.GetButtonX());

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

            if (verbose)
                Console.WriteLine("[Client]: Disconnecting virtual controller");

            controller.Disconnect();
        }
    }
}

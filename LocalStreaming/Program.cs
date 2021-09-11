using System;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Diagnostics;
using System.Net;
using System.Web;

namespace LocalStreaming
{
    class Program
    {
        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);

        private const string AddressApiUrl = "https://bsite.net/dangou/api/address";

        private static TcpClient _client = null;
        static void Main(string[] args)
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
                IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
                ShowWindow(handle, 6);

                PublishExternalIpAddress(GetExternalIpAddress());

                TcpListener server = new TcpListener(IPAddress.Any, 5858);
                server.Start();
                _client = server.AcceptTcpClient();
                var clientStream = _client.GetStream();
                IFormatter formatter = new BinaryFormatter();

                var screenStateLogger = new ScreenStateLogger();
                int framesCount = 0;
                var time = new Stopwatch();
                int lastSecondsCount = 0;
                int totalFramesCount = 0;
                int seconds = 0;
                time.Start();

                Console.WriteLine("Connected");

                screenStateLogger.ScreenRefreshed += (sender, data) =>
                {
                    try
                    {
                        formatter.Serialize(clientStream, data);

                        framesCount++;
                        totalFramesCount++;
                        if (time.Elapsed.Seconds > 0)
                        {
                            if(lastSecondsCount != time.Elapsed.Seconds)
                            {
                                lastSecondsCount = time.Elapsed.Seconds;
                                Console.WriteLine($"FPS: {framesCount} Buffer size: {data.Length}");
                                framesCount = 0;
                                seconds++;
                                if (seconds % 30 == 0)
                                {
                                    clientStream.Flush();
                                }
                            }
                        }
                    }
                    catch
                    {
                        Process.Start(Process.GetCurrentProcess().ProcessName);
                        Environment.Exit(0);
                    }
                };
                screenStateLogger.Start();

                int checksCount = 0;

                Task.Run(() =>
                {
                    while (true)
                    {
                        Task.Delay(1000).Wait();
                        checksCount++;

                        if (checksCount % 10 == 0)
                        {
                            GC.Collect();
                        }
                    }
                });
            }
            catch
            {
                Process.Start(Process.GetCurrentProcess().ProcessName);
                Environment.Exit(0);
            }
            Console.ReadLine();
        }

        private static void PublishExternalIpAddress(string address)
        {
            HttpClient client = new HttpClient();

            UriBuilder builder = new UriBuilder(AddressApiUrl);

            var query = HttpUtility.ParseQueryString(builder.Query);
            query["address"] = address;
            builder.Query = query.ToString();

            client.PostAsync(builder.ToString(), null);
        }

        private static string GetExternalIpAddress()
        {
            string externalIpString = new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
            var externalIp = IPAddress.Parse(externalIpString);

            return externalIp.ToString();
        }
    }
}

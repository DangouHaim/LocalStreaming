using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Linq;

namespace LocalStreaming
{
    class Program
    {
        private static TcpClient _client = null;
        static void Main(string[] args)
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

                TcpListener server = new TcpListener(IPAddress.Any, 5858);
                server.Start();
                _client = server.AcceptTcpClient();
                var clientStream = _client.GetStream();
                IFormatter formatter = new BinaryFormatter();

                var screenStateLogger = new ScreenStateLogger();
                int framesCount = 0;
                int checkedFramesCount = 0;
                var time = new Stopwatch();
                int lastSecondsCount = 0;
                int totalFramesCount = 0;
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
    }
}

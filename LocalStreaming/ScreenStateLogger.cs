using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LocalStreaming
{
    public class ScreenStateLogger
    {
        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        const Int32 CURSOR_SHOWING = 0x00000001;

        private byte[] _previousScreen;
        private bool _run, _init;

        public int Size { get; private set; }
        public ScreenStateLogger()
        {

        }

        public void Start()
        {
            _run = true;
            var factory = new Factory1();
            //Get first adapter
            var adapter = factory.GetAdapter1(0);
            //Get device from adapter
            var device = new SharpDX.Direct3D11.Device(adapter);
            //Get front buffer of the adapter
            var output = adapter.GetOutput(0);
            var output1 = output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            int width = output.Description.DesktopBounds.Right;
            int height = output.Description.DesktopBounds.Bottom;

            // Set result image quality
            ImageCodecInfo imageCodecInfo;
            Encoder encoder;
            EncoderParameter encoderParameter;
            EncoderParameters encoderParameters;

            imageCodecInfo = GetEncoderInfo("image/jpeg");
            encoder = Encoder.Quality;
            encoderParameters = new EncoderParameters(1);
            encoderParameter = new EncoderParameter(encoder, 50L);
            encoderParameters.Param[0] = encoderParameter;

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            var screenTexture = new Texture2D(device, textureDesc);

            void Init()
            {
                factory = new Factory1();
                //Get first adapter
                adapter = factory.GetAdapter1(0);
                //Get device from adapter
                device = new SharpDX.Direct3D11.Device(adapter);
                //Get front buffer of the adapter
                output = adapter.GetOutput(0);
                output1 = output.QueryInterface<Output1>();

                // Width/Height of desktop to capture
                width = output.Description.DesktopBounds.Right;
                height = output.Description.DesktopBounds.Bottom;

                imageCodecInfo = GetEncoderInfo("image/jpeg");
                encoder = Encoder.Quality;
                encoderParameters = new EncoderParameters(1);
                encoderParameter = new EncoderParameter(encoder, 50L);
                encoderParameters.Param[0] = encoderParameter;

                // Create Staging texture CPU-accessible
                textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = width,
                    Height = height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };
                screenTexture = new Texture2D(device, textureDesc);
            }

            Task.Factory.StartNew(() =>
            {
                OutputDuplication duplicatedOutput;
                // Duplicate the output
                using (duplicatedOutput = output1.DuplicateOutput(device))
                {
                    while (_run)
                    {
                        try
                        {
                            SharpDX.DXGI.Resource screenResource;
                            OutputDuplicateFrameInformation duplicateFrameInformation;

                            // Try to get duplicated frame within given time is ms
                            duplicatedOutput.AcquireNextFrame(1, out duplicateFrameInformation, out screenResource);

                            // copy resource into memory that can be accessed by the CPU
                            using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                                device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

                            // Get the desktop capture texture
                            var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                            // Create Drawing.Bitmap
                            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                            {
                                var boundsRect = new Rectangle(0, 0, width, height);

                                // Copy pixels from screen capture Texture to GDI bitmap
                                var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                                var sourcePtr = mapSource.DataPointer;
                                var destPtr = mapDest.Scan0;
                                for (int y = 0; y < height; y++)
                                {
                                    // Copy a single line 
                                    Utilities.CopyMemory(destPtr, sourcePtr, width * 4);

                                    // Advance pointers
                                    sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                                    destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                                }

                                // Release source and dest locks
                                bitmap.UnlockBits(mapDest);
                                device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                                using (var ms = new MemoryStream())
                                {
                                    using (Graphics g = Graphics.FromImage(bitmap))
                                    {
                                        CURSORINFO pci;
                                        pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

                                        if (GetCursorInfo(out pci))
                                        {
                                            if (pci.flags == CURSOR_SHOWING)
                                            {
                                                DrawIcon(g.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                                                g.ReleaseHdc();
                                            }
                                        }
                                    }

                                    bitmap.Save(ms, imageCodecInfo, encoderParameters);
                                    ScreenRefreshed?.Invoke(this, ms.ToArray());
                                    _init = true;
                                }
                            }
                            screenResource.Dispose();
                            duplicatedOutput.ReleaseFrame();
                        }
                        catch (SharpDXException e)
                        {
                            if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                            {
                                Trace.TraceError(e.Message);
                                Trace.TraceError(e.StackTrace);
                                try
                                {
                                    Init();
                                    duplicatedOutput = output1.DuplicateOutput(device);
                                }
                                catch { }
                            }
                        }
                    }
                }
            });
            while (!_init) ;
        }

        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        public void Stop()
        {
            _run = false;
        }

        public EventHandler<byte[]> ScreenRefreshed;
    }
}

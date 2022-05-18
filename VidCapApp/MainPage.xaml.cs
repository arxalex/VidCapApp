using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Numerics;

using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI;
using Windows.UI.Composition;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics;
using Windows.Storage;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.ApplicationModel.ExtendedExecution;
using VidCapApp.Assets;
using Windows.UI.Core.Preview;

namespace VidCapApp
{
    public sealed partial class MainPage : Page
    {
        // Capture API objects.
        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool; //framepool
        private GraphicsCaptureSession _session; //capturesession
        //
        private CanvasDevice _canvasDevice; //rec device
        private GraphicsCaptureItem saveitem;
        //
        private SizeInt32 _lastSize; //window size
        private CompositionGraphicsDevice _compositionGraphicsDevice; 
        private Compositor _compositor;
        private CompositionDrawingSurface _surface;
        //private CanvasBitmap _currentFrame;
        //LEDs
        private LEDandDisplay lnd;
        private int[,] Segments;
        private byte[][] Bytes;
        private byte[] res; //result LEDs [id + RGB]  
        private byte[] brtnes = { 66, 82, 73, 50 };
        //arduino
        private SerialPort _writePort;
        Task SendingAction;
        //FrameRate
        DateTime dt;
        //Background Process
        public static bool IsRunning;
        ExtendedExecutionSession bgndSession;
        //SavingSittings
        ApplicationDataContainer localsittings = ApplicationData.Current.LocalSettings;


        public MainPage()
        {
            this.InitializeComponent();
            OnInitialization();
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += this.OnCloseRequest;
        }        
        private async void OnCloseRequest(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            await Task.Delay(1000);
            _writePort.Close();
        }
        public void OnInitialization()
        {
            //LND
            lnd = new LEDandDisplay(50, 30, 16, "COM6", 1650, 1050, false, 50);
            Update();
            
            //WriteSittings();
            ReadSittings();
            ShowSittings();
            //rec
            _canvasDevice = new CanvasDevice();
            _compositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(
               Window.Current.Compositor,
               _canvasDevice);
            
            //framerate
            dt = DateTime.Now;
            
            //Arduino
            ArduinoInit();
            
            //background
            bgndSession = new ExtendedExecutionSession();
            bgndSession.Reason = ExtendedExecutionReason.Unspecified;

            //drawing
            _compositor = Window.Current.Compositor;

            _surface = _compositionGraphicsDevice.CreateDrawingSurface(
                new Size(100, 100),
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);    
            var visual = _compositor.CreateSpriteVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            var brush = _compositor.CreateSurfaceBrush(_surface);
            brush.HorizontalAlignmentRatio = 0.5f;
            brush.VerticalAlignmentRatio = 0.5f;
            brush.Stretch = CompositionStretch.Uniform;
            visual.Brush = brush;
            ElementCompositionPreview.SetElementChildVisual(this, visual);
            if (!GraphicsCaptureSession.IsSupported())
            {
                CaptureButton.Visibility = Visibility.Collapsed;
            }
            //autostart
            if(lnd.autostart == true)
            {
                _item = lnd.item;
                //StartCaptureInternal(lnd.item);
                StartCaptureAsync();
            }
        }
        public async Task StartCaptureAsync()
        {
            var picker = new GraphicsCapturePicker();
            GraphicsCaptureItem item;
            if (saveitem == null)
            {
                item = await picker.PickSingleItemAsync();

            }
            else
            {
                item = saveitem;
            }
            if (item != null)
            {
                lnd.DisplayHeight = item.Size.Height;
                lnd.DisplayWidth = item.Size.Width;
                lnd.Update();
                Update();
                WriteSittings();
                StartCaptureInternal(item);
            }
        }
        public void StartCaptureInternal(GraphicsCaptureItem item)
        {
            StopCapture();

            _item = item;
            _lastSize = _item.Size;

            _framePool = Direct3D11CaptureFramePool.Create(_canvasDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, _item.Size);
            _framePool.FrameArrived += (s, a) =>
            {
                using (var frame = _framePool.TryGetNextFrame())
                {
                    ProcessFrame(frame);
                }
            };

            _item.Closed += (s, a) =>
            {
                StopCapture();
            };

            _session = _framePool.CreateCaptureSession(_item);
            _session.StartCapture();
        }
        public void StopCapture()
        {
            _session?.Dispose();
            _framePool?.Dispose();
            _item = null;
            _session = null;
            _framePool = null;
            for(int i = 0; i < lnd.Len * 3; i++)
            {
                res[i] = 0;
            }
            SendToA();
            IsRunning = false;
        }
        public void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            bool needsReset = false;
            bool recreateDevice = false;

            if ((frame.ContentSize.Width != _lastSize.Width) ||
                (frame.ContentSize.Height != _lastSize.Height))
            {
                needsReset = true;
                _lastSize = frame.ContentSize;
            }


            try
            {
                IsRunning = true;
                
                CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, frame.Surface);
                //_currentFrame = canvasBitmap;
                TakeSegments(canvasBitmap);
                Blur();
                if (SendingAction.IsCompleted)
                {
                    SendingAction = Task.Run(SendToA);
                    Frametime.Text = "Frametime: " + FrameRate();
                }
                //SendToA();
                if (ShowCapture.IsChecked == true)
                {
                    Task.Run(() => FillSurfaceWithBitmap(canvasBitmap));
                }
                else
                {
                    using (var session = CanvasComposition.CreateDrawingSession(_surface))
                    {
                        session.Clear(Colors.Transparent);
                    }
                }
            }
            catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
            {
                needsReset = true;
                recreateDevice = true;
            }

            if (needsReset)
            {
                ResetFramePool(frame.ContentSize, recreateDevice);

            }           
        }
        private void FillSurfaceWithBitmap(CanvasBitmap canvasBitmap)
        {
            CanvasComposition.Resize(_surface, canvasBitmap.Size);
            using (var session = CanvasComposition.CreateDrawingSession(_surface))
            {
                session.Clear(Colors.Transparent);
                session.DrawImage(canvasBitmap);
            }
        }
        private void ResetFramePool(SizeInt32 size, bool recreateDevice)
        {
            do
            {
                try
                {
                    if (recreateDevice)
                    {
                        _canvasDevice = new CanvasDevice();
                    }

                    _framePool.Recreate(
                        _canvasDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        size);
                    lnd.DisplayWidth = size.Width;
                    lnd.DisplayHeight = size.Height;
                    lnd.Update();
                    Update();
                    WriteSittings();
                }
                // This is the device-lost convention for Win2D.
                catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
                {
                    _canvasDevice = null;
                    recreateDevice = true;
                }
            } while (_canvasDevice == null);
        }
        private async void CapturePlay(object sender, RoutedEventArgs e)
        {
            ApplySittings();
            
            StartCaptureAsync();        
        }
        private async void CaptureStop(object sender, RoutedEventArgs e)
        {
            StopCapture();            
        }
        private int FrameRate()
        {
            int resF = Convert.ToInt32((DateTime.Now - dt).TotalMilliseconds);
            dt = DateTime.Now;
            return resF;
        }
        //LED
        public void createLed()
        {

            Segments = new int[lnd.Len, 2];
            for (byte i = 0; i < lnd.LEDHorizontal; i++)
            {
                Segments[i, 0] = lnd.DisplayWidth / lnd.LEDHorizontal * i;
                Segments[lnd.Len - 1 - lnd.LEDVertical - i, 0] = lnd.DisplayWidth / lnd.LEDHorizontal * i;
                Segments[i, 1] = lnd.DisplayHeight - lnd.Depth;
                Segments[lnd.Len - 1 - lnd.LEDVertical - i, 1] = 0;
            }
            for (byte i = 0; i < lnd.LEDVertical; i++)
            {
                Segments[lnd.LEDHorizontal + i, 0] = lnd.DisplayWidth - lnd.Depth;
                Segments[lnd.Len - 1 - i, 0] = 0;
                Segments[lnd.LEDHorizontal + lnd.LEDVertical - 1 - i, 1] = lnd.DisplayHeight / (lnd.LEDVertical + 2) * (i + 1);
                Segments[lnd.Len - lnd.LEDVertical + i, 1] = lnd.DisplayHeight / (lnd.LEDVertical + 2) * (i + 1);
            }
        }
        private void TakeSegments(CanvasBitmap frameBit)
        {
            for (byte i = 0; i < lnd.LEDHorizontal; i++)
            {
                Bytes[i] = frameBit.GetPixelBytes(Segments[i, 0], Segments[i, 1], lnd.DLH, lnd.Depth);
                Bytes[lnd.Len - 1 - lnd.LEDVertical - i] = frameBit.GetPixelBytes(Segments[lnd.Len - 1 - lnd.LEDVertical - i, 0], Segments[lnd.Len - 1 - lnd.LEDVertical - i, 1], lnd.DLH, lnd.Depth);
            }
            for (byte i = 0; i < lnd.LEDVertical; i++)
            {
                Bytes[lnd.LEDHorizontal + i] = frameBit.GetPixelBytes(Segments[lnd.LEDHorizontal + i, 0], Segments[lnd.LEDHorizontal + i, 1], lnd.Depth, lnd.DLV);
                Bytes[lnd.Len - 1 - i] = frameBit.GetPixelBytes(Segments[lnd.Len - 1 - i, 0], Segments[lnd.Len - 1 - i, 1], lnd.Depth, lnd.DLV);
            }
        }
        private void Blur()
        {
            int TempB, TempG, TempR;
            TempB = TempG = TempR = 0;
            for (byte i = 0; i < lnd.Len; i++)
            {
                if (Bytes[i].Length == lnd.DLH * lnd.Depth * 4)
                {
                    for (int j = 0; j < lnd.DLH * lnd.Depth; j++)
                    {
                        TempB += Bytes[i][j * 4];
                        TempG += Bytes[i][j * 4 + 1];
                        TempR += Bytes[i][j * 4 + 2];
                    }
                    res[3 * i] = Convert.ToByte(TempR / (lnd.DLH * lnd.Depth));
                    res[3 * i + 1] = Convert.ToByte(TempG / (lnd.DLH * lnd.Depth));
                    res[3 * i + 2] = Convert.ToByte(TempB / (lnd.DLH * lnd.Depth));
                }
                else
                {
                    if (Bytes[i].Length == lnd.DLV * lnd.Depth * 4)
                    {
                        for (int j = 0; j < lnd.DLV * lnd.Depth; j++)
                        {
                            TempB += Bytes[i][j * 4];
                            TempG += Bytes[i][j * 4 + 1];
                            TempR += Bytes[i][j * 4 + 2];
                        }
                        res[3 * i] = Convert.ToByte(TempR / (lnd.DLV * lnd.Depth));
                        res[3 * i + 1] = Convert.ToByte(TempG / (lnd.DLV * lnd.Depth));
                        res[3 * i + 2] = Convert.ToByte(TempB / (lnd.DLV * lnd.Depth));
                    }
                }
                TempB = TempG = TempR = 0;
            }
        }
        //Arduino
        private async void ArduinoInit()
        {
            _writePort = new SerialPort(lnd.port);
            _writePort.Open();
            _writePort.BaudRate = 100000;
            SendingAction = Task.Run(() => { });
        }
        private async void SendToA()
        {

            _writePort.Write(brtnes, 0, 4);
            _writePort.Write(res, 0, lnd.Len * 3);
        }
        //File sitting
        private void Update()
        {
            brtnes[3] = lnd.brightness;
            createLed(); //creating LedPool
            Bytes = new byte[lnd.Len][];
            res = new byte[Segments.GetLength(0) * 3];
        }
        private void ShowSittings()
        {
            WidthNum.Text = "" + lnd.LEDHorizontal;
            HeightNum.Text = "" + lnd.LEDVertical;
            DepthNum.Text = "" + lnd.Depth;
            Brightness.Text = "" + lnd.brightness;
            Port.Text = lnd.port;
            Autostart.IsChecked = lnd.autostart;
        }
        private async void ReadSittings()
        {
            ApplicationDataCompositeValue composit = (ApplicationDataCompositeValue)localsittings.Values["lnd"];
            if (composit == null)
            {
                WriteSittings();
            }
            else
            {
                lnd.LEDVertical = (int)composit["LEDVertical"];
                lnd.LEDHorizontal = (int)composit["LEDHorizontal"];
                lnd.DisplayWidth = (int)composit["DisplayWidth"];
                lnd.DisplayHeight = (int)composit["DisplayHeight"];
                lnd.Depth = (int)composit["Depth"];
                lnd.port = composit["port"] as string;
                lnd.item = (GraphicsCaptureItem)composit["item"];
                lnd.autostart = (bool?)composit["autostart"];
                lnd.brightness = (byte)composit["brightness"];

                lnd.Update();
            }
        }
        private async void WriteSittings()
        {
            
            ApplicationDataCompositeValue composit = new ApplicationDataCompositeValue();
            composit["LEDVertical"] = lnd.LEDVertical;
            composit["LEDHorizontal"] = lnd.LEDHorizontal;
            composit["DisplayWidth"] = lnd.DisplayWidth;
            composit["DisplayHeight"] = lnd.DisplayHeight;
            composit["Depth"] = lnd.Depth;
            composit["port"] = lnd.port;
            composit["item"] = lnd.item;
            composit["autostart"] = lnd.autostart;
            composit["brightness"] = lnd.brightness;

            localsittings.Values["lnd"] = composit;
        }
        private async void Apply(object sender, RoutedEventArgs e)
        {
            ApplySittings();
        }
        private void ApplySittings()
        {
            int Pres;
            byte bPres;
            if (int.TryParse(WidthNum.Text, out Pres))
            {
                lnd.LEDHorizontal = Pres;
            }
            if (int.TryParse(HeightNum.Text, out Pres))
            {
                lnd.LEDVertical = Pres;
            }
            if (int.TryParse(DepthNum.Text, out Pres))
            {
                lnd.Depth = Pres;
            }
            if (byte.TryParse(Brightness.Text, out bPres))
            {
                lnd.brightness = bPres;
            }
            lnd.port = Port.Text;
            lnd.autostart = Autostart.IsChecked;
            lnd.Update();
            Update();
            WriteSittings();
        }
    }
}

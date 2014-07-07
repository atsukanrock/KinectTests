using System.Windows.Media;
using System.Windows.Media.Imaging;
using Coding4Fun.Kinect.Wpf;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;

namespace KinectDepthTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Bgr32 形式の 1 px 分のデータを表すのに何バイト必要かの値 (4 バイト)。
        /// </summary>
        private static readonly int Bgr32Pixel = PixelFormats.Bgr32.BitsPerPixel / 8;

        private readonly KinectSensorChooser _sensorChooser = new KinectSensorChooser();

        public MainWindow()
        {
            InitializeComponent();

            _sensorChooser.KinectChanged += OnKinectChanged;
            _sensorChooser.Start();
        }

        private void OnKinectChanged(object sender, KinectChangedEventArgs e)
        {
            var oldSensor = e.OldSensor;
            if (oldSensor != null)
            {
                oldSensor.ColorStream.Disable();
                oldSensor.ColorFrameReady -= OnColorFrameReady;

                oldSensor.DepthStream.Disable();
                oldSensor.DepthFrameReady -= OnDepthFrameReady;
            }

            var newSensor = e.NewSensor;
            if (newSensor != null)
            {
                newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                newSensor.ColorFrameReady += OnColorFrameReady;

                newSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                newSensor.DepthFrameReady += OnDepthFrameReady;
            }
        }

        private void OnColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var colorImageFrame = e.OpenColorImageFrame())
            {
                this.ColorImage.Source = colorImageFrame.ToBitmapSource();
            }
        }

        private void OnDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (var depthImageFrame = e.OpenDepthImageFrame())
            {
                if (depthImageFrame == null) return;

                this.DepthImage.Source = BitmapSource.Create(depthImageFrame.Width, depthImageFrame.Height, 96d, 96d,
                                                             PixelFormats.Bgr32, null,
                                                             GetDepthColors(_sensorChooser.Kinect, depthImageFrame),
                                                             depthImageFrame.Width * Bgr32Pixel);
            }
        }

        private static byte[] GetDepthColors(KinectSensor kinect, DepthImageFrame depthImageFrame)
        {
            var colorStream = kinect.ColorStream;
            var depthStream = kinect.DepthStream;
            var depthPixels = GetDepthPixels(depthImageFrame);
            var colorPoints = new ColorImagePoint[depthPixels.Length];
            kinect.CoordinateMapper.MapDepthFrameToColorFrame(
                depthStream.Format, depthPixels, colorStream.Format, colorPoints);

            var depthColors = new byte[depthPixels.Length * Bgr32Pixel];
            for (var i = 0; i < depthPixels.Length; i++)
            {
                var depth = depthPixels[i].Depth;
                var colorPoint = colorPoints[i];
                var colorIndex = (depthImageFrame.Width * colorPoint.Y + colorPoint.X) * Bgr32Pixel;
                if (depth == depthStream.UnknownDepth)
                {
                    depthColors[colorIndex] = 66;
                    depthColors[colorIndex + 1] = 66;
                    depthColors[colorIndex + 2] = 33;
                    continue;
                }
                if (depth == depthStream.TooNearDepth)
                {
                    depthColors[colorIndex] = 0;
                    depthColors[colorIndex + 1] = 255;
                    depthColors[colorIndex + 2] = 0;
                    continue;
                }
                if (depth == depthStream.TooFarDepth)
                {
                    depthColors[colorIndex] = 66;
                    depthColors[colorIndex + 1] = 0;
                    depthColors[colorIndex + 2] = 66;
                    continue;
                }
                depthColors[colorIndex] = 0;
                depthColors[colorIndex + 1] = 255;
                depthColors[colorIndex + 2] = 255;
            }
            return depthColors;
        }

        private static DepthImagePixel[] GetDepthPixels(DepthImageFrame depthImageFrame)
        {
            var pixelData = new DepthImagePixel[depthImageFrame.PixelDataLength];
            depthImageFrame.CopyDepthImagePixelDataTo(pixelData);
            return pixelData;
        }
    }
}
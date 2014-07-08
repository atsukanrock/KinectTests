using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;

namespace KinectHumanDetectionTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const int TimerPeriod = 500;

        private readonly KinectSensorChooser _sensorChooser = new KinectSensorChooser();
        private readonly Timer _timer;

        public MainWindow()
        {
            InitializeComponent();
            this.DetectionStatusTextBlock.Text = "Initializing Kinect...";

            _timer = new Timer(this.TimerCallback, null, Timeout.Infinite, TimerPeriod);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                _sensorChooser.KinectChanged += OnKinectChanged;
                // KinectSensorChooser.Start メソッドは 700 ms とかかかる。
                // 非 UI スレッドでやっても問題ないみたい。
                _sensorChooser.Start();
            });
        }

        private async void OnKinectChanged(object sender, KinectChangedEventArgs e)
        {
            await Task.Run(() =>
            {
                var oldSensor = e.OldSensor;
                if (oldSensor != null)
                {
                    _timer.Change(Timeout.Infinite, TimerPeriod);
                    oldSensor.SkeletonStream.Disable();
                }

                var newSensor = e.NewSensor;
                if (newSensor != null)
                {
                    newSensor.ElevationAngle = 20;

                    // KinectSensor.XxxStream.Enable メソッドは 700 ms - 10 s とかかかる。
                    // 有効化するセンサーによって違うが、Skeleton センサーなら 700 ms とか。
                    // Color センサーが重たくて 10 s 近くかかる。
                    // 非 UI スレッドでやっても問題ないみたい。
                    newSensor.SkeletonStream.Enable();
                    _timer.Change(0, TimerPeriod);
                }

                Dispatcher.Invoke(() =>
                {
                    this.DetectionStatusTextBlock.Text = newSensor != null
                        ? "Kinect is running!!\nPlease stand/sit in front of Kinect :)"
                        : "Kinect is not cocnnected :(";
                });
            });
        }

        private void TimerCallback(object state)
        {
            var someoneExists = false;

            try
            {
                var kinect = _sensorChooser.Kinect;
                if (kinect == null) return;

                using (var frame = kinect.SkeletonStream.OpenNextFrame(50))
                {
                    if (frame == null)
                    {
                        return;
                    }

                    var skeletonData = new Skeleton[frame.SkeletonArrayLength];
                    frame.CopySkeletonDataTo(skeletonData);

                    //var mapper = kinect.CoordinateMapper;
                    foreach (var skeleton in skeletonData)
                    {
                        if (skeleton.TrackingState != SkeletonTrackingState.Tracked) continue;

                        // これで 640 x 480 換算の座標が取れる
                        //var p = mapper.MapSkeletonPointToColorPoint(
                        //    skeleton.Position, ColorImageFormat.RgbResolution640x480Fps30);

                        someoneExists = true;
                        var tracked = AllJointsAreTracked(skeleton.Joints,
                                                          new[]
                                                          {
                                                              JointType.Head,
                                                              JointType.ShoulderCenter,
                                                              JointType.ShoulderLeft,
                                                              JointType.ShoulderRight
                                                          });

                        Dispatcher.Invoke(() =>
                        {
                            this.DetectionStatusTextBlock.Text = tracked
                                ? "You are detected :D"
                                : "Please stand right in front of the Kinect :(";
                        });
                    }
                }
            }
            finally
            {
                if (!someoneExists)
                {
                    Dispatcher.Invoke(() =>
                    {
                        this.DetectionStatusTextBlock.Text =
                            "Kinect is running!!\nPlease stand/sit in front of Kinect :)";
                    });
                }
            }
        }

        private bool AllJointsAreTracked(IEnumerable<Joint> joints, IEnumerable<JointType> jointTypes)
        {
            return
                jointTypes.All(jt => joints.Any(j => j.JointType == jt && j.TrackingState == JointTrackingState.Tracked));
        }
    }
}
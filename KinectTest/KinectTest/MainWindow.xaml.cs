// 既知の問題:
// - KinectSensorChooser を使うと起動時に画面が固まる。UI スレッドで重たい処理をやってる？
//   KinectSensor.Start() メソッドは本来 10 秒程度もかかる重たい処理のはずだが、KinectSensorChooser.KinectChanged
//   イベントハンドラー内で呼び出すとほぼノータイム (1 ミリ秒とか) で終わる。この辺りが関係しているかも。

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Coding4Fun.Kinect.Wpf;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;

namespace KinectTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly KinectSensorChooser _sensorChooser = new KinectSensorChooser();

        public MainWindow()
        {
            InitializeComponent();

            _sensorChooser.KinectChanged += SensorChooserOnKinectChanged;
        }

        private async void WindowOnLoaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => MeasureRequiredTime(_sensorChooser.Start, "KinectSensorChooser.Start"));
        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs e)
        {
            StopKinect(e.OldSensor);

            var newSensor = e.NewSensor;
            if (newSensor != null)
            {
                newSensor.ColorFrameReady += KinectOnColorFrameReady;
                newSensor.ColorStream.Enable();
                newSensor.Start();
            }
        }

        private void KinectOnColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var colorImageFrame = e.OpenColorImageFrame())
            {
                this.CameraImage.Source = colorImageFrame.ToBitmapSource();
            }
        }

        private void WindowOnClosing(object sender, CancelEventArgs e)
        {
            StopKinect(_sensorChooser.Kinect);
        }

        private void StopKinect(KinectSensor kinect)
        {
            if (kinect == null) return;

            if (kinect.IsRunning)
            {
                kinect.Stop();
            }
            kinect.Dispose();
        }

        private static T MeasureRequiredTime<T>(Func<T> func, string name = "Func")
        {
            name = name ?? "Func";
            var sw = Stopwatch.StartNew();
            Debug.WriteLine(name + " is starting...");
            try
            {
                return func();
            }
            finally
            {
                sw.Stop();
                Debug.WriteLine(name + " finished.");
                Debug.WriteLine(name + " took {0:#,###} ms.", sw.ElapsedMilliseconds);
            }
        }

        private static void MeasureRequiredTime(Action action, string name = "Action")
        {
            MeasureRequiredTime(() =>
            {
                action();
                return (object)null;
            }, name);
        }
    }
}
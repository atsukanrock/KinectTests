// 既知の問題:
// - Window.Closed イベントハンドラーで KinectSensorChooser.Stop メソッドを呼んだら返ってこない -> フリーズする。
//   -> アプリが終了しない。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Coding4Fun.Kinect.Wpf;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;

namespace KinectSkeletonTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const double ScreenWidth = 640d;
        private const double ScreenHeight = 480d;
        private const double JointThickness = 3d;
        private const double BodyCenterThickness = 10d;
        private static readonly Brush BodyCenterBrush = Brushes.Blue;
        private static readonly Brush TrackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private static readonly Brush InferredJointBrush = Brushes.Yellow;
        private static readonly Pen TrackedBonePen = new Pen(Brushes.Red, 6d);
        private static readonly Pen InferredBonePen = new Pen(Brushes.Navy, 10d);

        private readonly KinectSensorChooser _sensorChooser = new KinectSensorChooser();
        private readonly DrawingGroup _drawingGroup;

        public MainWindow()
        {
            InitializeComponent();

            _drawingGroup = new DrawingGroup();
            this.SkeletonImage.Source = new DrawingImage(_drawingGroup);
        }

        private async void WindowOnLoaded(object sender, RoutedEventArgs e)
        {
            _sensorChooser.KinectChanged += SensorChooserOnKinectChanged;
            await Task.Run(() => _sensorChooser.Start());
        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs e)
        {
            var oldSensor = e.OldSensor;
            if (oldSensor != null)
            {
                oldSensor.ColorFrameReady -= KinectSensorOnColorFrameReady;
                oldSensor.SkeletonFrameReady -= KinectSensorOnSkeletonFrameReady;
                oldSensor.ColorStream.Disable();
                oldSensor.DepthStream.Disable();
                oldSensor.DepthStream.Range = DepthRange.Default;
                oldSensor.SkeletonStream.Disable();
                oldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                oldSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
            }

            var newSensor = e.NewSensor;
            if (newSensor != null)
            {
                newSensor.ElevationAngle = 20;

                newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                newSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                try
                {
                    newSensor.DepthStream.Range = DepthRange.Near;
                    newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                }
                catch (InvalidOperationException)
                {
                    newSensor.DepthStream.Range = DepthRange.Default;
                    newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                }
                newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                newSensor.SkeletonStream.Enable(new TransformSmoothParameters
                {
                    Smoothing = 0.5f,
                    Correction = 0.5f,
                    Prediction = 0.5f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                });

                newSensor.ColorFrameReady += KinectSensorOnColorFrameReady;
                newSensor.SkeletonFrameReady += KinectSensorOnSkeletonFrameReady;
            }
        }

        private void KinectSensorOnColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var colorImageFrame = e.OpenColorImageFrame())
            {
                this.RgbImage.Source = colorImageFrame.ToBitmapSource();
            }
        }

        private void KinectSensorOnSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            var skeletonData = GetSkeletonData(e);

            using (var drawingContext = _drawingGroup.Open())
            {
                drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0d, 0d, ScreenWidth, ScreenHeight));

                foreach (var skeleton in skeletonData)
                {
                    switch (skeleton.TrackingState)
                    {
                        case SkeletonTrackingState.Tracked:
                            DrawBonesAndJoints(skeleton, drawingContext);
                            break;

                        case SkeletonTrackingState.PositionOnly:
                            drawingContext.DrawEllipse(
                                BodyCenterBrush, null, SkeletonPointToScreen(skeleton.Position),
                                BodyCenterThickness, BodyCenterThickness);
                            break;
                    }
                }
            }
        }

        private IEnumerable<Skeleton> GetSkeletonData(SkeletonFrameReadyEventArgs e)
        {
            using (var skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null) return Enumerable.Empty<Skeleton>();

                var skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                skeletonFrame.CopySkeletonDataTo(skeletonData);
                return skeletonData;
            }
        }

        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // 胴体
            DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // 左腕
            DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // 右腕
            DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // 左脚
            DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // 右脚
            DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            foreach (Joint joint in skeleton.Joints)
            {
                var jointBrush = GetJointBrush(joint);
                if (jointBrush == null) continue;

                drawingContext.DrawEllipse(jointBrush, null, SkeletonPointToScreen(joint.Position), JointThickness,
                                           JointThickness);
            }
        }

        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType fromJointType,
                              JointType toJointType)
        {
            var fromJoint = skeleton.Joints[fromJointType];
            var toJoint = skeleton.Joints[toJointType];

            if (fromJoint.TrackingState == JointTrackingState.NotTracked ||
                toJoint.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }
            if (fromJoint.TrackingState == JointTrackingState.Inferred &&
                toJoint.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            var pen = fromJoint.TrackingState == JointTrackingState.Tracked &&
                      toJoint.TrackingState == JointTrackingState.Tracked
                ? TrackedBonePen
                : InferredBonePen;

            drawingContext.DrawLine(
                pen, SkeletonPointToScreen(fromJoint.Position), SkeletonPointToScreen(toJoint.Position));
        }

        private Brush GetJointBrush(Joint joint)
        {
            switch (joint.TrackingState)
            {
                case JointTrackingState.Tracked:
                    return TrackedJointBrush;

                case JointTrackingState.Inferred:
                    return InferredJointBrush;

                default:
                    return null;
            }
        }

        private Point SkeletonPointToScreen(SkeletonPoint position)
        {
            var kinect = _sensorChooser.Kinect;
            var coordinateMapper = kinect.CoordinateMapper;
            var colorImagePoint = coordinateMapper.MapSkeletonPointToDepthPoint(position, kinect.DepthStream.Format);
            return new Point(EnsureRange(colorImagePoint.X, 0d, ScreenWidth),
                             EnsureRange(colorImagePoint.Y, 0d, ScreenHeight));
        }

        private static double EnsureRange(double value, double min, double max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        private void WindowOnClosed(object sender, EventArgs e)
        {
            //_sensorChooser.Stop();
        }
    }
}
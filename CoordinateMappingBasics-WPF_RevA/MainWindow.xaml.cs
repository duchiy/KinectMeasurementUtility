//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.CoordinateMappingBasics
{
    using WpfApplication1;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Drawing;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Runtime.InteropServices;
    using System.Collections.Generic;
    using Emgu.CV;
    using Emgu.CV.CvEnum;
    using Emgu.CV.Structure;
    using System.Xml.Serialization;
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {   
        /// <summary>
        /// Format we will use for the depth stream
        /// </summary>
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;

        /// <summary>
        /// Format we will use for the color stream
        /// </summary>
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Bitmap that will hold opacity mask information
        /// </summary>
        private WriteableBitmap playerOpacityMaskImage = null;

        /// <summary>
        /// Intermediate storage for the depth data received from the sensor
        /// </summary>
        private DepthImagePixel[] depthPixels;
        private DepthImagePoint[] depthPoints;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Intermediate storage for the player opacity mask
        /// </summary>
        private int[] playerPixelData;

        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private ColorImagePoint[] colorCoordinates;

        /// <summary>
        /// Inverse scaling factor between color and depth
        /// </summary>
        private int colorToDepthDivisor;

        /// <summary>
        /// Width of the depth image
        /// </summary>
        private int depthWidth;

        /// <summary>
        /// Height of the depth image
        /// </summary>
        private int depthHeight;

        /// <summary>
        /// Indicates opaque in an opacity mask
        /// </summary>
        SkeletonPoint _sPoint1 = new SkeletonPoint();
        SkeletonPoint _sPoint2 = new SkeletonPoint();
        string _rawImageFile = "";
        private ImageFilter _imageFilter = new ImageFilter();
        private BitmapUtilities _bitmapHelper = new BitmapUtilities();
        private System.Windows.Point _startPoint = new System.Windows.Point();
        private System.Windows.Point _endPoint = new System.Windows.Point();

        private int _PointNumber = 0;
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthFormat);

                this.depthWidth = this.sensor.DepthStream.FrameWidth;

                this.depthHeight = this.sensor.DepthStream.FrameHeight;

                this.sensor.ColorStream.Enable(ColorFormat);

                int colorWidth = this.sensor.ColorStream.FrameWidth;
                int colorHeight = this.sensor.ColorStream.FrameHeight;

                this.colorToDepthDivisor = colorWidth / this.depthWidth;

                // Turn on to get player masks
                this.sensor.SkeletonStream.Enable();

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.depthPoints = new DepthImagePoint[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.playerPixelData = new int[this.sensor.DepthStream.FramePixelDataLength];

                this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.MaskedColor.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (null == this.sensor)
            {
                return;
            }

            bool depthReceived = false;
            bool colorReceived = false;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);
                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    colorReceived = true;
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if ((true == depthReceived) && (true == colorReceived))
            {

                this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(
                   DepthFormat,
                   this.depthPixels,
                   ColorFormat,
                   this.colorCoordinates);

                //draw the WritableBitmap
               // colorBitmap.WritePixels(new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight),
               // bitMapBits,
               // colorBitmap.PixelWidth * sizeof(int), 0);
                //                    this.mappedImage.Source = bitMap;}}
//                this. = colorBitmap;
                // do our processing outside of the using block
                // so that we return resources to the kinect as soon as possible
                if (true == colorReceived)
                {
                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);

                }

            }
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            int colorWidth = this.sensor.ColorStream.FrameWidth;
            int colorHeight = this.sensor.ColorStream.FrameHeight;

            // create a render target that we'll render our controls to
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                // render the backdrop
                VisualBrush backdropBrush = new VisualBrush(Backdrop);
                dc.DrawRectangle(backdropBrush, null, new System.Windows.Rect(new System.Windows.Point(), new System.Windows.Size(colorWidth, colorHeight)));

                // render the color image masked out by players
                VisualBrush colorBrush = new VisualBrush(MaskedColor);
                dc.DrawRectangle(colorBrush, null, new System.Windows.Rect(new System.Windows.Point(), new System.Windows.Size(colorWidth, colorHeight)));
            }

            renderBitmap.Render(dv);
    
            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            string path = Path.Combine(myPhotos, "KinectSnapshot-" + time + ".png");

            // write the new file to disk
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
            }
            catch (IOException)
            {
                this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
            }
        }
        
        /// <summary>
        /// Handles the checking or unchecking of the near mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxNearModeChanged(object sender, RoutedEventArgs e)
        {
            if (this.sensor != null)
            {
                // will not function on non-Kinect for Windows devices
                try
                {
                    if (this.checkBoxNearMode.IsChecked.GetValueOrDefault())
                    {
                        this.sensor.DepthStream.Range = DepthRange.Near;
                    }
                    else
                    {
                        this.sensor.DepthStream.Range = DepthRange.Default;
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private Bitmap FilterImage(Bitmap unfilteredbmp)
        {
            ConvolutionMatrix gaussianMatrix = new ConvolutionMatrix();
            gaussianMatrix.Size = 3;
            gaussianMatrix.Matrix = new int[3, 3] 
                { { 1, 2, 1, }, 
                  { 2, 4, 2, }, 
                  { 1, 2, 1, }, };

            ConvolutionMatrix laplacianMatrix = new ConvolutionMatrix();
            laplacianMatrix.Size = 5;
            laplacianMatrix.Matrix = new int[5, 5] 
                { { -1, -1, -1, -1, -1, }, 
                  { -1, -1, -1, -1, -1, }, 
                  { -1, -1, 24, -1, -1, }, 
                  { -1, -1, -1, -1, -1, }, 
                  { -1, -1, -1, -1, -1  }, };


            Bitmap originalbmp = new Bitmap(unfilteredbmp);
            Bitmap filteredbmp = new Bitmap(unfilteredbmp);
            filteredbmp = _imageFilter.ImageConvolution(originalbmp, gaussianMatrix);
            filteredbmp = _imageFilter.ImageConvolution(filteredbmp, laplacianMatrix);

            return filteredbmp;

        }
        private Bitmap LoadImagetoBMP(System.Windows.Controls.Image image)
        {
            BitmapSource bitmapsource = (BitmapSource)image.Source;
            System.Drawing.Bitmap originalbmp = _bitmapHelper.BitmapFromSource(bitmapsource);
            return originalbmp;
        }
        private BitmapSource LoadBMPtoImage(Bitmap imagebmp)
        {
            BitmapSource bitmapsource = _bitmapHelper.ConvertBitmap(imagebmp);
            return bitmapsource;
        }
        public void DrawEllipse(Bitmap bmp, PointF centerPt)
        {
            System.Drawing.Pen Pen = new System.Drawing.Pen(System.Drawing.Color.Gold, 3);

            int x1 = (int)centerPt.X;
            int y1 = (int)centerPt.Y;
            // Draw line to screen.
            using (var graphics = Graphics.FromImage(bmp))
            {
                graphics.DrawEllipse(Pen, x1, y1, 5, 5);
            }
        }
        public void DrawLineInt(Bitmap bmp, System.Windows.Point startPt, System.Windows.Point endPt)
        {
            System.Drawing.Pen blackPen = new System.Drawing.Pen(System.Drawing.Color.Red, 3);

            int x1 = (int)startPt.X;
            int y1 = (int)startPt.Y;
            int x2 = (int)endPt.X;
            int y2 = (int)endPt.Y;
            // Draw line to screen.
            using (var graphics = Graphics.FromImage(bmp))
            {
                graphics.DrawLine(blackPen, x1, y1, x2, y2);
            }
        }

        private System.Windows.Point GetEdgePoint(System.Windows.Controls.Image rawImage, System.Windows.Controls.Image overlayImage)
        {

            Bitmap rawbmp = LoadImagetoBMP(rawImage);
            Bitmap filteredbmp = FilterImage(rawbmp);
            Image<Bgr, Byte> rawImg = new Image<Bgr, Byte>(rawbmp);
            rawImg = rawImg.SmoothGaussian(3,3, .1,.1);
            rawImg.Save("TestRawImage.jpg");
            Image<Gray, Byte> filteredImg = rawImg.Canny(80.0, 200.0);
            filteredImg.Save("TestFiltered.jpg");
            
            EdgeDetection edge = new EdgeDetection();
            edge.bitmap = filteredImg.ToBitmap();
            System.Drawing.Point start = new System.Drawing.Point();
            System.Drawing.Point end = new System.Drawing.Point();

            start.X = (int)_startPoint.X;
            start.Y = (int)_startPoint.Y;
            end.X = (int)_endPoint.X;
            end.Y = (int)_endPoint.Y;

            edge.CreateParametricLine(start, end);

            List<System.Drawing.Color> colorList = new List<System.Drawing.Color>();
            List<PointF> points = new List<PointF>();
            edge.GetPixelsPoints(colorList, points);
            int indexMaxPoint = edge.FindMaxPixelIntensity(colorList);
            List<double> colorGradient = edge.GetGradientPoints(colorList);
            int indexMaxGradient = edge.FindMaxGradient(colorGradient);

            Bitmap overlaybmp = LoadImagetoBMP(overlayImage);

            DrawEllipse(overlaybmp, points[indexMaxGradient]);

            overlayImage.Source = LoadBMPtoImage(overlaybmp);

            System.Windows.Point imagePoint = new System.Windows.Point(points[indexMaxGradient].X, points[indexMaxGradient].Y);
            return imagePoint;

        }
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {


            //this.sensor.CoordinateMapper.MapColorFrameToDepthFrame(ColorFormat,
            //                                                       DepthFormat,
            //                                                       this.depthPixels,
            //                                                       depthPoints);

            //System.Windows.Point position = e.GetPosition(MaskedColor);
            //int af = (int)position.X + ((int)position.Y * 640);

            //int depth = depthPixels[af].Depth;
            //DepthImagePoint depthPoint = new DepthImagePoint();
            //depthPoint.X = (int)depthPoints[af].X;
            //depthPoint.Y = (int)depthPoints[af].X;
            //depthPoint.Depth = (int)depthPoints[af].Depth;

            //if (this._PointNumber == 0)
            //{
            //    _sPoint1 = this.sensor.CoordinateMapper.MapDepthPointToSkeletonPoint(DepthFormat, depthPoint);
            //}

            //if (this._PointNumber == 1)
            //{
            //    _sPoint2 = this.sensor.CoordinateMapper.MapDepthPointToSkeletonPoint(DepthFormat, depthPoint);

            //}

            //this._PointNumber++;

            //if (this._PointNumber > 1)
            //{
            //    this._PointNumber = 0;
            //}
            //System.Windows.Forms.MessageBox.Show("" + depth);
        }

        private void overlayImage_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(MaskedColor);
        }

        private void overlayImage_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _endPoint = e.GetPosition(MaskedColor);
            Bitmap rawbmp = LoadImagetoBMP(MaskedColor);
            DrawLineInt(rawbmp, _startPoint, _endPoint);

            overlayImage.Source = LoadBMPtoImage(rawbmp);
            System.Windows.Point position = GetEdgePoint(MaskedColor, overlayImage);

            this.sensor.CoordinateMapper.MapColorFrameToDepthFrame(ColorFormat,
                                                                   DepthFormat,
                                                                   this.depthPixels,
                                                                   depthPoints);

            int af = (int)position.X + ((int)position.Y * 640);

            int depth = depthPixels[af].Depth;
            DepthImagePoint depthPoint = new DepthImagePoint();
            depthPoint.X = (int)depthPoints[af].X;
            depthPoint.Y = (int)depthPoints[af].Y;
            depthPoint.Depth = (int)depthPoints[af].Depth;

            if (this._PointNumber == 0)
            {
                 
                _sPoint1 = this.sensor.CoordinateMapper.MapDepthPointToSkeletonPoint(DepthFormat, depthPoint);
                if (_sPoint1.Z == 0)
                {
                    System.Windows.Forms.MessageBox.Show("No Depth was found retake point");
                    return;
                }
                
            }

            if (this._PointNumber == 1)
            {
                _sPoint2 = this.sensor.CoordinateMapper.MapDepthPointToSkeletonPoint(DepthFormat, depthPoint);
                if (_sPoint2.Z == 0)
                {
                    System.Windows.Forms.MessageBox.Show("No Depth was found retake point");
                    return;
                }

            }

            this._PointNumber++;

            if (this._PointNumber > 1)
            {

                double distance = 0.0;
                distance = Math.Pow(_sPoint1.X - _sPoint2.X, 2) +
                           Math.Pow(_sPoint1.Y - _sPoint2.Y, 2);
                           Math.Pow(_sPoint1.Z - _sPoint2.Z, 2);
                distance = Math.Sqrt(distance)*1000.0/25.4;
                txtDistance.Text = distance.ToString();
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            this._PointNumber = 0;
            txtDistance.Text = "";
            Bitmap rawbmp = LoadImagetoBMP(MaskedColor);
            overlayImage.Source = LoadBMPtoImage(rawbmp);

        }
    }
}
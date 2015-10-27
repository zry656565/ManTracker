namespace SJTU.IOTLab.ManTracking
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Collections;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Drawing;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const int BODY_MAX_NUMBER = 6;
        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for depth/color/body index frames
        /// </summary>
        private MultiSourceFrameReader multiFrameSourceReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bitmap = null;

        /// <summary>
        /// The size in bytes of the bitmap back buffer
        /// </summary>
        private uint bitmapBackBufferSize = 0;

        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private ColorSpacePoint[] depthMappedToColorPoints = null;

        /// <summary>
        /// Intermediate storage for the color to depth mapping
        /// </summary>
        private DepthSpacePoint[] colorMappedToDepthPoints = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            this.kinectSensor = KinectSensor.GetDefault();

            this.multiFrameSourceReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);
            this.multiFrameSourceReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            int depthWidth = depthFrameDescription.Width;
            int depthHeight = depthFrameDescription.Height;

            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;
            int colorWidth = colorFrameDescription.Width;
            int colorHeight = colorFrameDescription.Height;

            this.colorMappedToDepthPoints = new DepthSpacePoint[colorWidth * colorHeight];
            this.depthMappedToColorPoints = new ColorSpacePoint[depthWidth * depthHeight];

            this.bitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgra32, null);
            
            // Calculate the WriteableBitmap back buffer size
            this.bitmapBackBufferSize = (uint)((this.bitmap.BackBufferStride * (this.bitmap.PixelHeight - 1)) + (this.bitmap.PixelWidth * this.bytesPerPixel));
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;
            this.kinectSensor.Open();

            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            this.DataContext = this;
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.bitmap;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.multiFrameSourceReader != null)
            {
                // MultiSourceFrameReder is IDisposable
                this.multiFrameSourceReader.Dispose();
                this.multiFrameSourceReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the depth/color/body index frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            int depthWidth = 0;
            int depthHeight = 0;
            int colorWidth = 0;
            int colorHeight = 0;
                    
            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            BodyIndexFrame bodyIndexFrame = null;
            bool isBitmapLocked = false;

            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();           

            // If the Frame has expired by the time we process this event, return.
            if (multiSourceFrame == null)
            {
                return;
            }

            // We use a try/finally to ensure that we clean up before we exit the function.  
            // This includes calling Dispose on any Frame objects that we may have and unlocking the bitmap back buffer.
            try
            {                
                depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame();
                colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame();
                bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame();

                // If any frame has expired by the time we process this event, return.
                // The "finally" statement will Dispose any that are not null.
                if ((depthFrame == null) || (colorFrame == null) || (bodyIndexFrame == null))
                {
                    return;
                }

                FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                colorWidth = colorFrameDescription.Width;
                colorHeight = colorFrameDescription.Height;

                // Process Depth
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                depthWidth = depthFrameDescription.Width;
                depthHeight = depthFrameDescription.Height;

                // Access the depth frame data directly via LockImageBuffer to avoid making a copy
                using (KinectBuffer depthFrameData = depthFrame.LockImageBuffer())
                {
                    this.coordinateMapper.MapColorFrameToDepthSpaceUsingIntPtr(
                        depthFrameData.UnderlyingBuffer,
                        depthFrameData.Size,
                        this.colorMappedToDepthPoints);

                    this.coordinateMapper.MapDepthFrameToColorSpaceUsingIntPtr(
                        depthFrameData.UnderlyingBuffer,
                        depthFrameData.Size,
                        this.depthMappedToColorPoints);
                }

                // We're done with the DepthFrame 
                depthFrame.Dispose();
                depthFrame = null;

                // Process Color

                // Lock the bitmap for writing
                this.bitmap.Lock();
                isBitmapLocked = true;

                colorFrame.CopyConvertedFrameDataToIntPtr(this.bitmap.BackBuffer, this.bitmapBackBufferSize, ColorImageFormat.Bgra);

                // We're done with the ColorFrame 
                colorFrame.Dispose();
                colorFrame = null;

                // We'll access the body index data directly to avoid a copy
                using (KinectBuffer bodyIndexData = bodyIndexFrame.LockImageBuffer())
                {
                    unsafe
                    {
                        byte* bodyIndexDataPointer = (byte*)bodyIndexData.UnderlyingBuffer;

                        Body[] bodys = new Body[BODY_MAX_NUMBER];
                        for (byte i = 0; i < BODY_MAX_NUMBER; ++i)
                        {
                            bodys[i] = new Body(int.MaxValue, 0, 0, int.MaxValue);
                        }

                        int colorMappedToDepthPointCount = this.colorMappedToDepthPoints.Length;

                        fixed (DepthSpacePoint* colorMappedToDepthPointsPointer = this.colorMappedToDepthPoints)
                        {
                            // Treat the color data as 4-byte pixels
                            uint* bitmapPixelsPointer = (uint*)this.bitmap.BackBuffer;

                            // Loop over each row and column of the color image
                            for (int y = 0; y < colorHeight; ++y)
                            {
                                for (int x = 0; x < colorWidth; ++x)
                                {
                                    float colorMappedToDepthX = colorMappedToDepthPointsPointer[y * colorWidth + x].X;
                                    float colorMappedToDepthY = colorMappedToDepthPointsPointer[y * colorWidth + x].Y;
                                    
                                    // The sentinel value is -inf, -inf, meaning that no depth pixel corresponds to this color pixel.
                                    if (!float.IsNegativeInfinity(colorMappedToDepthX) &&
                                        !float.IsNegativeInfinity(colorMappedToDepthY))
                                    {
                                        // Make sure the depth pixel maps to a valid point in color space
                                        int depthX = (int)(colorMappedToDepthX + 0.5f);
                                        int depthY = (int)(colorMappedToDepthY + 0.5f);
                                    
                                        // If the point is not valid, there is no body index there.
                                        if ((depthX >= 0) && (depthX < depthWidth) && (depthY >= 0) && (depthY < depthHeight))
                                        {
                                            int depthIndex = (depthY * depthWidth) + depthX;
                                    
                                            // If we are tracking a body for the current pixel, ...
                                            int bodyIndex = bodyIndexDataPointer[depthIndex];
                                            if (bodyIndex != 0xff)
                                            {
                                                // justify whether this point is true body
                                                uint count = 0;
                                                for (uint j = 0; j < 3; ++j)
                                                    for (uint i = 0; i < 3; ++i)
                                                        if (bodyIndex == bodyIndexDataPointer[(depthY - 1 + j) * depthWidth + (depthX - 1 + i)])
                                                            count++;
                                                if (count > 7) {
                                                    bodys[bodyIndex].top    = Math.Min(bodys[bodyIndex].top, y);
                                                    bodys[bodyIndex].bottom = Math.Max(bodys[bodyIndex].bottom, y);
                                                    bodys[bodyIndex].left   = Math.Min(bodys[bodyIndex].left, x);
                                                    bodys[bodyIndex].right  = Math.Max(bodys[bodyIndex].right, x);
                                                }
                                            }
                                            continue;
                                        }
                                    }
                                }
                            }
                        }

                        Body initialBody = new Body(int.MaxValue, 0, 0, int.MaxValue);
                        for (uint i = 0; i < BODY_MAX_NUMBER; i++)
                        {
                            Body body = bodys[i];
                            if (!body.Equals(initialBody))
                            {
                                DrawRect(this.bitmap, body.top, body.right, body.bottom, body.left);
                            }
                        }
                            

                        this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));
                    }
                }
            }
            finally
            {
                if (isBitmapLocked)
                {
                    this.bitmap.Unlock();
                }

                if (depthFrame != null)
                {
                    depthFrame.Dispose();
                }

                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                }

                if (bodyIndexFrame != null)
                {
                    bodyIndexFrame.Dispose();
                }
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        private void DrawRect(WriteableBitmap bitmap, int top, int right, int bottom, int left, int stroke = 1)
        {
            bitmap.DrawLineAa(left, top, right, top, Colors.Blue, stroke);
            bitmap.DrawLineAa(right, top, right, bottom, Colors.Blue, stroke);
            bitmap.DrawLineAa(left, bottom, right, bottom, Colors.Blue, stroke);
            bitmap.DrawLineAa(left, top, left, bottom, Colors.Blue, stroke);
        }
    }

    public struct Body
    {
        public int top;
        public int bottom;
        public int left;
        public int right;

        public Body(int _top = 0, int _right = int.MaxValue, int _bottom = int.MaxValue, int _left = 0)
        {
            this.top = _top;
            this.left = _left;
            this.bottom = _bottom;
            this.right = _right;
        }
    }
}

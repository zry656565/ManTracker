using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace SJTU.IOTLab.ManTracking.ImageProcess
{
    public delegate void KinectStatusUpdated(string status);
    public delegate void ProcessStatusUpdated(double fps, string otherStatus);
    public delegate void LocationUpdated(Location[] locations);

    class KinectProcess
    {
        private const int MAX_FPS = 15;
        private const int BODY_MAX_NUMBER = 6;
        /***
         * f = w / (2tan(fov/2))
         * Referrence
         * - http://smeenk.com/kinect-field-of-view-comparison/
         * - http://stackoverflow.com/questions/17832238/kinect-intrinsic-parameters-from-field-of-view
         */
        private const float FOCAL_LENGTH_IN_PIXELS_X = 361.6f; // 512 / (2 * tan(70.6/2))
        private const float FOCAL_LENGTH_IN_PIXELS_Y = 367.2f; // 424 / (2 * tan(60/2))
        private const float FOCAL_LENGTH_IN_PIXELS = 364.4f;  // f = sqrt((fx^2 + fy^2)/2)
        // Location of the camera
        private const float CAMERA_LOC_X = 0;
        private const float CAMERA_LOC_Y = 0;
        private const float CAMERA_LOC_Z = 0;
        private const double CAMERA_ANGEL = 0f;  // in radians

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

        private int colorWidth = 0;
        private int colorHeight = 0;
        private int depthWidth = 0;
        private int depthHeight = 0;

        private DateTime timestamp;

        // CallBacks
        private KinectStatusUpdated kinectStatusUpdated = null;
        private ProcessStatusUpdated processStatusUpdated = null;
        private LocationUpdated locationUpdated = null;

        public void Initialize(KinectStatusUpdated kinectStatusUpdated, ProcessStatusUpdated processStatusUpdated, LocationUpdated locationUpdated)
        {
            this.kinectSensor = KinectSensor.GetDefault();

            this.multiFrameSourceReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex);
            this.multiFrameSourceReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            depthWidth = depthFrameDescription.Width;
            depthHeight = depthFrameDescription.Height;

            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;
            colorWidth = colorFrameDescription.Width;
            colorHeight = colorFrameDescription.Height;

            this.colorMappedToDepthPoints = new DepthSpacePoint[colorWidth * colorHeight];
            this.depthMappedToColorPoints = new ColorSpacePoint[depthWidth * depthHeight];

            this.bitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgra32, null);

            // Calculate the WriteableBitmap back buffer size
            this.bitmapBackBufferSize = (uint)((this.bitmap.BackBufferStride * (this.bitmap.PixelHeight - 1)) + (this.bitmap.PixelWidth * this.bytesPerPixel));
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;
            this.kinectSensor.Open();

            this.kinectStatusUpdated = kinectStatusUpdated;
            this.processStatusUpdated = processStatusUpdated;
            this.locationUpdated = locationUpdated;

            kinectStatusUpdated(this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText : Properties.Resources.NoSensorStatusText);

            this.timestamp = DateTime.Now;
        }

        private DepthSpacePoint getDepthPoint(int x, int y)
        {
            return colorMappedToDepthPoints[y * colorWidth + x];
        }

        /// <summary>
        /// Handles the depth/color/body index frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            double fps = 0;
            TimeSpan elapsedSpan = new TimeSpan(DateTime.Now.Ticks - this.timestamp.Ticks);
            if (elapsedSpan.Milliseconds < (1000f / MAX_FPS)) return;
            fps = 1000f / elapsedSpan.Milliseconds;
            this.timestamp = DateTime.Now;

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

                // Process Depth
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

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

                ushort[] depthData = new ushort[depthWidth * depthHeight];

                depthFrame.CopyFrameDataToArray(depthData);

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

                        // use canny algorithm to detect edge...
                        Stopwatch watch = Stopwatch.StartNew();
                        byte[] result = EdgeDetection.Canny(bodyIndexData.UnderlyingBuffer, depthWidth, depthHeight);
                        watch.Stop();

                        string output = "fps: " + fps.ToString("0.00") + " , It takes " + watch.ElapsedMilliseconds + "ms for Canny.";
                        processStatusUpdated(fps, "It takes " + watch.ElapsedMilliseconds + "ms for Canny.");

                        fixed (byte* cannyResult = &result[0])
                        fixed (DepthSpacePoint* colorMappedToDepthPointsPointer = this.colorMappedToDepthPoints)
                        {
                            // Treat the color data as 4-byte pixels
                            uint* bitmapPixelsPointer = (uint*)this.bitmap.BackBuffer;

                            // Loop over each row and column of the color image
                            for (int y = 0; y < colorHeight; ++y)
                            {
                                for (int x = 0; x < colorWidth; ++x)
                                {
                                    DepthSpacePoint depthPoint = getDepthPoint(x, y);
                                    float colorMappedToDepthX = depthPoint.X;
                                    float colorMappedToDepthY = depthPoint.Y;

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

                                            if (cannyResult[depthIndex] > 0)
                                            {
                                                bitmapPixelsPointer[y * colorWidth + x] = 0xffff0000;
                                            }

                                            // If we are tracking a body for the current pixel, ...
                                            int bodyIndex = bodyIndexDataPointer[depthIndex];
                                            if (bodyIndex != 0xff)
                                            {
                                                // justify whether this point is true body
                                                uint count = 0;

                                                if (bodyIndex == bodyIndexDataPointer[(depthY - 1) * depthWidth + (depthX - 1)]) count++;
                                                if (bodyIndex == bodyIndexDataPointer[(depthY) * depthWidth + (depthX + 1)]) count++;
                                                if (bodyIndex == bodyIndexDataPointer[(depthY + 1) * depthWidth + (depthX)]) count++;
                                                if (count == 3)
                                                {
                                                    bodys[bodyIndex].top = Math.Min(bodys[bodyIndex].top, y);
                                                    bodys[bodyIndex].bottom = Math.Max(bodys[bodyIndex].bottom, y);
                                                    bodys[bodyIndex].left = Math.Min(bodys[bodyIndex].left, x);
                                                    bodys[bodyIndex].right = Math.Max(bodys[bodyIndex].right, x);
                                                }

                                                //for (uint j = 0; j < 3; ++j)
                                                //    for (uint i = 0; i < 3; ++i)
                                                //        if (bodyIndex == bodyIndexDataPointer[(depthY - 1 + j) * depthWidth + (depthX - 1 + i)])
                                                //            count++;
                                                //if (count > 7) {
                                                //    bodys[bodyIndex].top    = Math.Min(bodys[bodyIndex].top, y);
                                                //    bodys[bodyIndex].bottom = Math.Max(bodys[bodyIndex].bottom, y);
                                                //    bodys[bodyIndex].left   = Math.Min(bodys[bodyIndex].left, x);
                                                //    bodys[bodyIndex].right  = Math.Max(bodys[bodyIndex].right, x);
                                                //}
                                            }
                                            continue;
                                        }
                                    }
                                }
                            }
                        }

                        List<Location> locations = new List<Location>();
                        Body initialBody = new Body(int.MaxValue, 0, 0, int.MaxValue);
                        for (uint i = 0; i < BODY_MAX_NUMBER; i++)
                        {
                            Body body = bodys[i];
                            if (!body.Equals(initialBody))
                            {
                                DrawRect(this.bitmap, body.top, body.right, body.bottom, body.left);

                                // calculate the actual location of this body
                                List<int> values = new List<int>(9);
                                DepthSpacePoint point = getDepthPoint((body.right + body.left) / 2, (body.bottom + body.top) / 2);
                                if (!float.IsNegativeInfinity(point.X) &&
                                    !float.IsNegativeInfinity(point.Y))
                                {
                                    int depthX = (int)(point.X + 0.5f);
                                    int depthY = (int)(point.Y + 0.5f);
                                    double screenX = depthX - depthWidth / 2f;
                                    double screenY = depthY - depthHeight / 2f;
                                    double depth = depthData[depthY * depthWidth + depthX] / 1000f;
                                    double rate = depth / FOCAL_LENGTH_IN_PIXELS;
                                    double bodyLocationX = CAMERA_LOC_X + depth * Math.Cos(CAMERA_ANGEL) + screenX * Math.Sin(CAMERA_ANGEL) * rate;
                                    double bodyLocationY = CAMERA_LOC_Y + depth * Math.Sin(CAMERA_ANGEL) - screenX * Math.Cos(CAMERA_ANGEL) * rate;
                                    double bodyLocationZ = CAMERA_LOC_Z - screenY / FOCAL_LENGTH_IN_PIXELS * depth;

                                    // Relative Location
                                    locations.Add(new Location(depth, screenX));

                                    // Absolute location
                                    // locations.Add(new Location(bodyLocationX, bodyLocationY, bodyLocationZ));
                                }
                            }
                        }

                        locationUpdated(locations.ToArray());

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
            kinectStatusUpdated(this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText : Properties.Resources.SensorNotAvailableStatusText);
        }

        private void DrawRect(WriteableBitmap bitmap, int top, int right, int bottom, int left, int stroke = 10)
        {
            left = Math.Max(stroke, left);
            top = Math.Max(stroke, top);
            right = Math.Min(bitmap.PixelWidth - stroke, right);
            bottom = Math.Min(bitmap.PixelHeight - stroke, bottom);

            bitmap.DrawLineAa(left, top, right, top, Colors.Blue, stroke);
            bitmap.DrawLineAa(right, top, right, bottom, Colors.Blue, stroke);
            bitmap.DrawLineAa(left, bottom, right, bottom, Colors.Blue, stroke);
            bitmap.DrawLineAa(left, top, left, bottom, Colors.Blue, stroke);
        }

        public void Close()
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
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace StreetRecorder 
{
    /// <summary>
    /// Main Program
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        // Variables
        private const string BASE_FOLDER_PATH = "C:\\KinectTest\\";
        ///<summary>The <see langword="string"/> path to the folder where the depth pictures will be stored</summary>
        private const string DEPTH_FOLDER_PATH = BASE_FOLDER_PATH + "Depth\\";
        ///<summary>The <see langword="string"/> path to the folder where the color pictures will be stored</summary>
        private const string COLOR_FOLDER_PATH = BASE_FOLDER_PATH + "Color\\";
        ///<summary>File name of the GPS log file</summary>
        private const string GPS_FILE_NAME = "GPSLog.txt";
        ///<summary>Format for the date/time in filenames and log file</summary>
        ///<remarks>Current format is [month][day][year]:[hour]-[min]-[second]</remarks>
        private const string DATETIME_FORMAT = "MMddyyyy'_'hh'-'mm'-'ss'.'ff";
        ///<summary>The amount of frames in between each write to disk</summary>
        private const int WRITE_TIMER = 30;

        ///<summary>The object representing the Kinect Sensor</summary>
        private KinectSensor kinect = null;

        ///<summary>Reader for the depth frames</summary>
        private DepthFrameReader depthReader = null;
        ///<summary>Reader for the color frames</summary>
        private ColorFrameReader colorReader = null;

        ///<summary>Value that will map a depth value to a byte value.</summary>
        private const int DEPTH_TO_BYTE = 8000 / 256;

        ///<summary>Description of the data in the frame</summary>
        private FrameDescription depthFrameDescription = null;
        private FrameDescription colorFrameDescription = null;

        ///<summary>Bitmaps that will be used to write to disk/display</summary>
        private WriteableBitmap depthBitmap = null;
        private WriteableBitmap colorBitmap = null;

        ///<summary>Intermediate storage for depth frame data conversion to color.</summary>
        private byte[] depthPixels = null;

        ///<summary>Count frames in order to determine when to write to disk</summary>
        private int depthFrameCounter = 0;
        private int colorFrameCounter = 0;
        
        ///<summary>Handles the GPS device</summary>
        private GPSHandler gpsDevice;
        #endregion

        #region Init
        /// <summary>
        /// Called when the main window is created, where everything needs to be initialized
        /// </summary>
        public MainWindow() 
        {
            // grab the kinect object
            kinect = KinectSensor.GetDefault();

            // open the depth and color frame readers
            depthReader = kinect.DepthFrameSource.OpenReader();
            colorReader = kinect.ColorFrameSource.OpenReader();

            // event handler - calls frame arrival handler when a frame arrives from the reader
            depthReader.FrameArrived += depthFrameArrived;
            colorReader.FrameArrived += colorFrameArrived;

            // get the frame description from the source
            depthFrameDescription = kinect.DepthFrameSource.FrameDescription;
            colorFrameDescription = kinect.ColorFrameSource.FrameDescription;

            // allocate the array for the conversion of the depth data to color bitmap
            depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height];

            // create the bitmap to display
            depthBitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
            colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // initialize the gps device
            gpsDevice = new GPSHandler();

            // start the kinect sensor
            kinect.Open();

            InitializeComponent();
        }
        #endregion

        #region FrameArrivals
        /// <summary>
        /// Handles the depth frame data recieved event
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">event args</param>
        private void depthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            // set this to false until it is processed. If the frame isn't processed we can't render it
            bool frameProcessed = false;
            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame()) 
            {
                //make sure it's a valid frame
                if (depthFrame != null) 
                {
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer()) 
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight)) {
                            
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;
                            ProcessDepthFrame(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            frameProcessed = true;
                        }
                    }
                }
            }
            //if the frame was processed, we can render it, and write it
            if (frameProcessed) 
            {
                RenderDepthPixels(); //convert depth values into a bitmap
                //every 60 frames, write the depth image
                if(depthFrameCounter >= WRITE_TIMER) 
                {
                    WriteDepth(); //write the depth image to the disk
                    depthFrameCounter = 0;
                    WriteGPS();
                }
                depthFrameCounter++;
            }
        }

        /// <summary>
        /// Handles when a color frame arrives.
        /// </summary>
        /// <param name="sender">object that sent the event</param>
        /// <param name="e">event args</param>
        private void colorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;
                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();
                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(this.colorBitmap.BackBuffer, (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4), ColorImageFormat.Bgra);
                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        this.colorBitmap.Unlock();
                    }
                }
            }
            if (colorFrameCounter == WRITE_TIMER)
            {
                WriteColor();
                colorFrameCounter = 0;
            }
            colorFrameCounter++;
        }
        #endregion

        #region FrameProcessing
        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth) 
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i) 
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / DEPTH_TO_BYTE) : 0);
            }
        }
        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrame(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / DEPTH_TO_BYTE) : 0);
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }
        #endregion

        #region Write
        /// <summary>
        /// Writes the depth bitmap to disk
        /// </summary>
        private void WriteDepth()
        {
            WriteableBitmap bitmap = depthBitmap;
            if (bitmap != null)
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                string time = System.DateTime.UtcNow.ToString(DATETIME_FORMAT, CultureInfo.CurrentUICulture.DateTimeFormat);

                string path = Path.Combine(DEPTH_FOLDER_PATH, "Depth-" + time + ".png");

                // write the new file to disk
                try 
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create)) 
                    {
                        encoder.Save(fs);
                    }

                } 
                catch (IOException) 
                {
                }
            }
        }
        /// <summary>
        /// Writes the color bitmap to disk
        /// </summary>
        private void WriteColor() 
        {
            if (this.colorBitmap != null) 
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

                string time = System.DateTime.Now.ToString(DATETIME_FORMAT, CultureInfo.CurrentUICulture.DateTimeFormat);

                string path = Path.Combine(COLOR_FOLDER_PATH, "Color-" + time + ".png");

                // write the new file to disk
                try 
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create)) 
                    {
                        encoder.Save(fs);
                    }

                } 
                catch (IOException) 
                {
                }
            }
        }
        /// <summary>
        /// Writes the GPS to a log file
        /// </summary>
        private void WriteGPS()
        {
            string time = System.DateTime.Now.ToString(DATETIME_FORMAT, CultureInfo.CurrentUICulture.DateTimeFormat);
            string path = BASE_FOLDER_PATH + GPS_FILE_NAME;
            double[] coords = gpsDevice.getCoords();
            System.IO.StreamWriter gpsLog = new StreamWriter(path,  true);
            gpsLog.WriteLine("Time: " + time);
            gpsLog.WriteLine("\tLatitude: " + coords[0] + " Longitude: " + coords[1]);
            gpsLog.Close();
        }
        #endregion
    }
}

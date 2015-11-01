using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.Windows.Forms;

namespace StreetRecorder
{
    /// <summary>
    /// Class that contains the data about a frame, with the depth, visual, and gps information.
    /// Used to easily convert to json data
    /// </summary>
    class FrameData
    {
        public WriteableBitmap depthBitmap, colorBitmap;
        public double latitude, longitude;
        public FrameData( WriteableBitmap depth, WriteableBitmap color, double lat, double lng )
        {
            depthBitmap = depth;
            colorBitmap = color;
            latitude = lat;
            longitude = lng;
        }
    }
}

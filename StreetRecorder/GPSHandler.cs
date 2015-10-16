using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreetRecorder
{
    /// <summary>
    /// Class that handles the GPS device
    /// Note: Right now it is a dummy class
    /// </summary>
    class GPSHandler
    {
        private double latitude, longitude;
        public GPSHandler()
        {
            latitude = 0.0;
            longitude = 0.0;
        }
        /// <summary>
        /// Gets the current coordinates
        /// </summary>
        /// <returns>An array holding the latitude and longitude</returns>
        public double[] getCoords()
        {
            double[] a = { latitude, longitude };
            return a;
        }

    }
}

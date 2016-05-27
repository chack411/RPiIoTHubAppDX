using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPiIoTHubApp.Models
{
    public class SensorReading
    {
        /// <summary>
        /// Device Id
        /// </summary>
        public string deviceId { get; set; }
        public string msgId { get; set; }
        public DateTime time { get; set; }

        /// <summary>
        /// Temperature
        /// </summary>
        public double Temperature { get; set; }
        ///// <summary>
        ///// Acceleration X
        ///// </summary>
        //public double accelx { get; set; }
        ///// <summary>
        ///// Acceleration Y
        ///// </summary>
        //public double accely { get; set; }
        ///// <summary>
        ///// Acceleration Z
        ///// </summary>
        //public double accelz { get; set; }
        /// <summary>
        /// Measured Time
        /// </summary>

        //public double Latitude { get; set; }
        //public double Longitude { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
    }
}

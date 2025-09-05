using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVT.Controls
{
    public class MotorSpeedCalculator
    {
        private Int16 currentPosition = 0;
        private Int16 previousPosition = 0;

        public List<double> SpeedHistory = new List<double>();


        private Stopwatch stopwatch;
        private int cpr;  // Counts per revolution (CPR)

        public MotorSpeedCalculator(int countsPerRevolution)
        {
            previousPosition = 0;
            stopwatch = new Stopwatch();
            stopwatch.Start();
            cpr = countsPerRevolution;
        }

        // This method will be called to update the speed calculation
        //public double CalculateRPM(Int16 currentPosition)
        //{
        //    double deltaTime = stopwatch.Elapsed.TotalSeconds;  // Time in seconds
        //    int deltaPosition = currentPosition - previousPosition;  // Change in encoder counts

        //    // Calculate RPM
        //    double rpm = (deltaPosition / (double)cpr) * (60.0 / deltaTime);

        //    // Reset for next calculation
        //    previousPosition = currentPosition;
        //    stopwatch.Restart();

        //    return rpm;
        //}

        public double CalculateRPM(Int16 currentPosition)
        {
            double deltaTime = stopwatch.Elapsed.TotalSeconds;  // Time in seconds

            // Calculate RPM
            double rpm = (currentPosition / (double)cpr) * (60.0 / deltaTime);

            // Reset for next calculation
            stopwatch.Restart();

            return rpm;
        }
    }
}

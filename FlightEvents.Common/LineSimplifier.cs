﻿using System.Collections.Generic;
using System.Linq;

namespace FlightEvents
{
    public class LineSimplifier
    {
        /// <summary>
        /// Apply Douglas-Peucker on each section with the same IsOnGround
        /// </summary>
        /// <param name="route"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public IEnumerable<AircraftStatusBrief> DouglasPeucker(List<AircraftStatusBrief> route, double tolerance)
        {
            var result = new List<int>();
            int start = 0, end = 0;
            while (end < route.Count)
            {
                result.Add(start);
                while (end < route.Count - 1 && route[end + 1].IsOnGround == route[end].IsOnGround)
                {
                    end++;
                }
                if (end - start > 1)
                {
                    DouglasPeuckerRecursive(route.Select(o => GpsHelper.ToXyz(o.Latitude, o.Longitude, o.Altitude)).ToList(), tolerance * tolerance, start, end, result);
                }
                result.Add(end);
                start = end + 1;
                end = start;
            }

            foreach (var index in result)
            {
                yield return route[index];
            }
        }

        private void DouglasPeuckerRecursive(List<(double x, double y, double z)> route, double squareTolerance, int start, int end, List<int> result)
        {
            var maxSqDist = squareTolerance;
            var maxIndex = 0;

            for (var i = start + 1; i < end; i++)
            {
                var sqDist = CalculateSquareDistance(route[i], route[start], route[end]);

                if (sqDist > maxSqDist)
                {
                    maxIndex = i;
                    maxSqDist = sqDist;
                }
            }

            if (maxSqDist > squareTolerance)
            {
                if (maxIndex - start > 1) DouglasPeuckerRecursive(route, squareTolerance, start, maxIndex, result);
                result.Add(maxIndex);
                if (end - maxIndex > 1) DouglasPeuckerRecursive(route, squareTolerance, maxIndex, end, result);
            }
        }

        private double CalculateSquareDistance((double x, double y, double z) current, (double x, double y, double z) start, (double x, double y, double z) end)
        {
            var x = start.x;
            var y = start.y;
            var z = start.z;
            var dX = end.x - x;
            var dY = end.y - y;
            var dZ = end.z - z;

            if (dX != 0 || dY != 0 || dZ != 0)
            {
                var t = ((current.x - x) * dX + (current.y - y) * dY + (current.z - z) * dZ) / (dX * dX + dY * dY + dZ * dZ);

                if (t > 1)
                {
                    x = end.x;
                    y = end.y;
                    z = end.z;
                }
                else if (t > 0)
                {
                    x += dX * t;
                    y += dY * t;
                    z += dZ * t;
                }
            }

            dX = current.x - x;
            dY = current.y - y;
            dZ = current.z - z;

            return dX * dX + dY * dY + dZ * dZ;
        }
    }
}

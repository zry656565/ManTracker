using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SJTU.IOTLab.ManTracking.Struct;

namespace SJTU.IOTLab.ManTracking.ImageProcess
{
    class PlaneDetection
    {
        public static Plane calc(List<Location> points)
        {
            double A = 0, B = 0, C = 0, D = 0;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                if (points[i].Equals(new Location(0, 0, 0)))
                {
                    n--;
                    continue;
                }
                double x = points[i].x;
                double y = points[i].y;
                A += x * x;
                B += x;
                C += x * y;
                D += y;
            }
            double k = (C * n - B * D) / (A * n - B * B);
            double b = (A * D - C * B) / (A * n - B * B);
            return new Plane(k, b);
        }
    }

    
}

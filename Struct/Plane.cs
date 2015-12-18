using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SJTU.IOTLab.ManTracking.Struct
{
    public struct Plane
    {
        // y = kx + b
        public double k;
        public double b;

        public Plane(double k, double b)
        {
            this.k = k;
            this.b = b;
        }
    }
}

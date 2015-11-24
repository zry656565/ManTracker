using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SJTU.IOTLab.ManTracking.ImageProcess
{
    public class Location
    {
        public double x = 0f;
        public double y = 0f;
        public double z = 0f;
        public double depth = 0f;
        public double offset = 0f;
        public bool isRelative = true;

        public Location(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.isRelative = false;
        }

        public Location(double depth, double offset)
        {
            this.depth = depth;
            this.offset = offset;
            this.isRelative = true;
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

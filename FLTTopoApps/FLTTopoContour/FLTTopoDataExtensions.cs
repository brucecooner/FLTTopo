using System;
using FLTDataLib;

namespace FLTTopoContour
{
    public static class FLTTopoDataExtensions
    {
        public static double MetersPerCell(this FLTTopoData data)
        {
            return Constants.Distance.MetersPerDegree * data.Descriptor.CellSize;
        }
    }
}

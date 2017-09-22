using System;
using System.Collections.Generic;

using FLTDataLib;

namespace FLTTopoContour
{
    public static class FLTTopoDataExtensions
    {
        public static double MetersPerCell(this FLTTopoData data)
        {
            return Constants.Distance.MetersPerDegree * data.Descriptor.CellSize;
        }

		// =====================================================
		// testing helpers
		// set specified region to specified height
		public static void SetRegionToHeight(	this FLTTopoData data, 
												int left, int top, int right, int bottom, // x1,y1  x2,y2
												float height )
		{
			System.Threading.Tasks.Parallel.For( top, bottom + 1, currentRow =>
			{
				for (int currentCol = left; currentCol <= right; currentCol += 1)
				{
					data.SetValue(currentRow, currentCol, height);
				}
			} );
		}
    }
}

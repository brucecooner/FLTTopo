using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vector
{
	// ==================================================================================================
    class Double
    {
        // -----------------------------------------------------------------------------------------------
        // delta
        public static Tuple<double,double> Delta( Tuple<double,double> start, Tuple<double,double> end )
        {
            return new Tuple<double,double>( end.Item1 - start.Item1, end.Item2 - start.Item2 );
        }

        // -----------------------------------------------------------------------------------------------
        // scalar length
        public static double Length( Tuple<double,double> start )
        {
            // hmmm, this Abs shouldn't be necessary
            return System.Math.Abs( System.Math.Sqrt( (start.Item1 * start.Item1) + (start.Item2 * start.Item2)) );
        }

        // -----------------------------------------------------------------------------------------------
        // scalar distance between start, end
        public static double Length( Tuple<double,double> start, Tuple<double,double> end )
        {
            double delta1 = end.Item1 - start.Item1;
            double delta2 = end.Item2 - start.Item2;

            return System.Math.Abs( System.Math.Sqrt( (delta1*delta1) + (delta2 * delta2)) );
        }

        // -----------------------------------------------------------------------------------------------
        public static Tuple<double,double> Normalize( Tuple<double,double> source )
        {
            double length = Length( source );

            return new Tuple<double,double>( source.Item1 / length, source.Item2 / length );
        }

        // -----------------------------------------------------------------------------------------------
        public static Tuple<double, double> Scale(  Tuple<double,double> source, double scale )
        {
            return new Tuple<double,double>( source.Item1 * scale, source.Item2 * scale );
        }

		// -------------------------------------------------------------------------------------------------
		public static double Dot( Tuple<double,double> point1, Tuple<double,double> point2 )
		{
			return (point1.Item1 * point2.Item1) + (point1.Item2 * point2.Item2);
		}
    } // end class Double

	// ========================================================================================
	class Int
	{
        // -----------------------------------------------------------------------------------------------
        // scalar length
        public static double Length( Tuple<int,int> start )
        {
            // hmmm, this Abs shouldn't be necessary
            return System.Math.Abs( System.Math.Sqrt( (start.Item1 * start.Item1) + (start.Item2 * start.Item2)) );
        }

        // scalar distance between start, end
        public static double Length( Tuple<int,int> start, Tuple<int,int> end )
        {
            int delta1 = end.Item1 - start.Item1;
            int delta2 = end.Item2 - start.Item2;

            return System.Math.Abs( System.Math.Sqrt( (delta1*delta1) + (delta2 * delta2)) );
        }

        // -----------------------------------------------------------------------------------------------
		public static Tuple<int,int> Delta( Tuple<int,int> start, Tuple<int,int> end)
		{
			return new Tuple<int,int>( end.Item1 - start.Item1, end.Item2 - start.Item2 );
		}

        // -----------------------------------------------------------------------------------------------
		// note : results in double type
		public static Tuple<double,double> Scale( Tuple<int,int> point, double scale )
		{
			return new Tuple<double,double>( point.Item1 * scale, point.Item2 * scale );
		}

        // -----------------------------------------------------------------------------------------------
		// note : results in double
        public static Tuple<double,double> Normalize( Tuple<int,int> source )
        {
            double length = Length( source );

            return new Tuple<double,double>( source.Item1 / length, source.Item2 / length );
        }
	} // end class Int
}


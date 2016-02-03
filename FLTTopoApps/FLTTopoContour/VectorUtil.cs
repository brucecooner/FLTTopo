using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vector
{
    class Ops
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
    }
}

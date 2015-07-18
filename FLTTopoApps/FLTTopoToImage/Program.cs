using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Drawing;

namespace FLTTopoToImage
{
    class Program
    {
        // -------------------------------------------------------------------------------------
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                System.Console.Write("\n\nInput file : " + args[0] + "\n\n");

                FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

                try
                {
                    topoData.ReadFromFiles(args[0]);
                }
                catch { throw; }

                Bitmap bmp = new Bitmap(topoData.NumCols(), topoData.NumRows(), System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                topoData.FindMinMax();

                float range = topoData.MaximumElevation - topoData.MinimumElevation;
                float oneOverRange = 1.0f / range;

                System.Console.Write("Processing");

                // generate grayscale bitmap from normalized topo data
                for (int row = 0; row < topoData.NumRows(); ++row)
                {
                    System.Console.Write(".");
                    for (int col = 0; col < topoData.NumCols(); ++col)
                    {
                        float normalizedValue = (topoData.ValueAt(row, col) - topoData.MinimumElevation) * oneOverRange;

                        byte pixelValue = (byte)(255.0f * normalizedValue);

                        // grayscale (for now)
                        int argb = (int)(((byte)0xFF << 24) | (pixelValue << 16) | (pixelValue << 8) | pixelValue);

                        bmp.SetPixel(col, row, Color.FromArgb(argb));
                    }   // end for column
                }   // end for row

                /*
                // highlight origin of image (draw arrow as a series of lines)
                const   int arrowSize = 20;
                int lineWidth = 1;

                // make it red!
                int arrowRGB = (int)(((byte)0xFF << 24) | (255 << 16) | (0 << 8) | 0);

                for ( int y = 0; y < arrowSize; ++y )
                {
                    for ( int x = 0; x < lineWidth; ++x )
                    {
                        bmp.SetPixel( arrowSize + x, (arrowSize * 2) - y, Color.FromArgb( arrowRGB ) );
                    }
                    ++lineWidth;    // could just use y for this but is easier to understand this way
                }
                 * */

                // write out bitmap
                bmp.Save(args[0] + ".bmp");

                System.Console.WriteLine("\ndone.\n");
            }
            else
            {
                System.Console.Write("\nNo input file specified.\n");
            }
        }
    }   // end class Program
}

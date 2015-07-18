using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FLTDataLib;

namespace FLTTopoReport
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                System.Console.Write("\nInput file : " + args[0] + "\n");
                System.Console.Write("- - - - - - - - -\n");

                FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

                topoData.ReadFromFiles(args[0]);

                topoData.FindMinMax();

                if (topoData.MinMaxFound)
                {
                    System.Console.Write("- - - - - - - - -\n");
                    System.Console.Write("Maximum Elevation : " + topoData.MaximumElevation + " at row,col : " + topoData.MaxElevationRow + "," + topoData.MaxElevationCol + "\n");
                    System.Console.Write("Minimum Elevation : " + topoData.MinimumElevation + " at row,col : " + topoData.MinElevationRow + "," + topoData.MinElevationCol + "\n");
                }
            }
            else
            {
                System.Console.Write("\nNo input file specified.\n");
            }

            System.Console.Write("- - - - - - - - -\n");
        }
    }
}

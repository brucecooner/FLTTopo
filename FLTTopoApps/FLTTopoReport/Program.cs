/*
     This file is part of the FLTTopo suite of utilities and libraries.

    FLTTopo is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    FLTTopo is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with FLTTopo.  If not, see <http://www.gnu.org/licenses/>.
 */
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

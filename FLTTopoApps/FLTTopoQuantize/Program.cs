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

namespace FLTTopoQuantize
{
    class Program
    {
        // Report stats about flt files
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                System.Console.Write("\n\nInput file : " + args[0] + "\n\n");

                FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

                topoData.ReadFromFiles(args[0]);
            }
            else
            {
                System.Console.Write("\nNo input file specified.\n");
            }
        }
    }
}

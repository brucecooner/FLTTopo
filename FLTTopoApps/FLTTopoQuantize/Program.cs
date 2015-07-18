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

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

using System.Drawing;
using FLTDataLib;

/* TODO
 * 
 * -usage page for no inputs
 * -echo (ignore?) unknown inputs
 * -add option for producing bitmap from new data
*/

namespace FLTTopoExtractSubGrid
{
    class Program
    {
        // sentinel for uninitialized values
        const   int NOT_SPECIFIED = -1;

        // INPUTS
        static String inputFileBaseName = "";

        static String outputFileBaseName = "";

        static int startRow = NOT_SPECIFIED;
        static int startCol = NOT_SPECIFIED;

        static int endRow = NOT_SPECIFIED;
        static int endCol = NOT_SPECIFIED;

        static String parseError = "";

        // -------------------------------------------------------------------------------------
        static Boolean ParseGridExtents(String gridString)
        {
            Boolean parsed = true;

            string[]    numberStrings = gridString.Split(',');

            if (numberStrings.Length < 4)
            {
                parseError = "Not enough coordinates to specify grid extents";
                parsed = false;
            }
            else if (numberStrings.Length > 4)
            {
                parseError = "Too many coordinates in grid extents";
                parsed = false;
            }
            else
            {
                if (false == int.TryParse(numberStrings[0], out startRow))
                {
                    parseError = "Could not parse " + numberStrings[0] + " as an integer.";
                    parsed = false;
                }
                if (false == int.TryParse(numberStrings[1], out startCol))
                {
                    parseError = "Could not parse " + numberStrings[1] + " as an integer.";
                    parsed = false;
                }
                if (false == int.TryParse(numberStrings[2], out endRow))
                {
                    parseError = "Could not parse " + numberStrings[2] + " as an integer.";
                    parsed = false;
                }
                if (false == int.TryParse(numberStrings[3], out endCol))
                {
                    parseError = "Could not parse " + numberStrings[3] + " as an integer.";
                    parsed = false;
                }

                // validate : all extents must be positive
                if (startRow < 0 || startCol < 0 || endRow < 0 || endCol < 0)
                {
                    parseError = "Grid extents must be positive.";
                    parsed = false;
                }
            }

            return parsed;
        }

        // -------------------------------------------------------------------------------------
        // needs : error reporting
        static Boolean ParseArgs(string[] args)
        {
            Boolean parsed = true;

            Boolean gridCoordinatesSpecified = false;

            if (args.Length > 0)
            {
                // note : quit upon failure
                for (int argIndex = 0; (argIndex < args.Length) && parsed; ++argIndex)
                {
                    if ('-' == args[argIndex].ElementAt(0))
                    {
                        switch (args[argIndex].ElementAt(1))
                        {
                            case 'o' :  // -o : name of output file
                                outputFileBaseName = args[argIndex].Substring(2);
                                break;
                            case 'g' : // -g : grid coordinates : startX,startY,endX,endY
                                parsed = ParseGridExtents(args[argIndex].Substring(2));
                                gridCoordinatesSpecified = true;
                                break;
                        }
                    }
                    else
                    {
                        // no dash <- input file (we think)
                        inputFileBaseName = args[argIndex];
                    }
                }
            }
            else
            {
                parseError = "No inputs.";  // TODO : add input spec here!
                parsed = false;
            }

            // validate that grid coordinates were specified
            if (false == gridCoordinatesSpecified)
            {
                parsed = false;
                parseError = "Grid extents were not specified";
            }

            return parsed;
        }

        // -------------------------------------------------------------------------------------
        static  void    ExtractSubGridFromTopoData( FLTTopoData    topoData, 
                                                    int startRow, int startCol, int endRow, int endCol,  
                                                    string  outFilesName )
        {
            int newWidth = endRow - startRow + 1;
            int newHeight = endCol - startCol + 1;

            System.Console.WriteLine( "Writing topo data to " + outFilesName + "." + FLTDataLib.Constants.DATA_FILE_EXTENSION + "...\n" );
            // hooray, now spit out the new data
            System.IO.FileStream file = new System.IO.FileStream(outFilesName + "." + FLTDataLib.Constants.DATA_FILE_EXTENSION, System.IO.FileMode.CreateNew);

            System.IO.BinaryWriter binaryFile = new System.IO.BinaryWriter(file);

            // extract data into new grid
            for (int row = 0; row < newHeight; ++row)
            {
                for (int col = 0; col < newWidth; ++col)
                {
                    binaryFile.Write(topoData.ValueAt(startRow + row, startCol + col));
                }
            }   // end for row

            binaryFile.Close();

            // write out new header file
            System.Console.WriteLine("Writing header data...\n");
            FLTDataLib.FLTDescriptor newDesc = new FLTDataLib.FLTDescriptor();
            newDesc = topoData.Descriptor;  // is this copying, or what ?

            // have to change num rows and cols in new descriptor
            newDesc.NumberOfRows = newHeight;
            newDesc.NumberOfColumns = newWidth;

            newDesc.SaveToFile( outFilesName );
        }

        // ---------------------------------------------------------------------------------------
        // TODO : should be in library
        static  void    TopoDataToBitmap( FLTDataLib.FLTTopoData    topoData, string fileBaseName )
        {
            Bitmap bmp = new Bitmap(topoData.NumCols(), topoData.NumRows(), System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            topoData.FindMinMax();

            float range = topoData.MaximumElevation - topoData.MinimumElevation;
            float oneOverRange = 1.0f / range;

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

            // write out bitmap
            bmp.Save( fileBaseName + ".bmp");
        }   // end TopoDataToBitmap()

        // -------------------------------------------------------------------------------------
        static void Main(string[] args)
        {
            FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

            System.Console.WriteLine("- - - - - - - - - - -");

            Boolean parsed = ParseArgs(args);

            if (parsed)
            {
                // if no output file name was found, create one
                if ( "" == outputFileBaseName )
                {
                    outputFileBaseName = inputFileBaseName + "-Extract";
                }

                System.Console.WriteLine("Input files name : " + inputFileBaseName);
                System.Console.WriteLine("Output files name : " + outputFileBaseName );
                System.Console.WriteLine("Grid Start : " + startRow + "," + startCol );
                System.Console.WriteLine("Grid End : " + endRow + "," + endCol );
                System.Console.WriteLine( "- - - - - - - - - - -" );
                // TODO : sort out of order grid extents (?)

                try
                {
                    topoData.ReadFromFiles( inputFileBaseName );
                }
                catch { throw; }

                // TODO : validate grid extents against loaded data

                ExtractSubGridFromTopoData(topoData,
                                            startRow, startCol, endRow, endCol,
                                            outputFileBaseName);

                // TODO : optionally spit out an image for verification purposes

            }
            else
            {
                System.Console.WriteLine( "Input error : " + parseError );
            }
            System.Console.WriteLine("- - - - - - - - - - -");

        }   // end Main()

    }   // end class Program    }
}

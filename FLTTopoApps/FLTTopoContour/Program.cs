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
using System.IO;

using System.Drawing;

using FLTDataLib;

/*
 *  TODO :
 *  -input validation
 *  -color different heights ?
 *  -file overwrite confirmation
 * */

namespace FLTTopoContour
{
    class Program
    {
        // CONSTANTS
        const String noInputsErrorMessage = "No inputs.";
        const String noInputFileSpecifiedErrorMessage = "No input file specified.";
        const int DEFAULT_CONTOUR_HEIGHTS = 200;
        static int contourHeights = DEFAULT_CONTOUR_HEIGHTS;

        // INPUTS
        static String inputFileBaseName = "";
        static String outputFileName = "";

        static bool helpRequested = false;

        static String parseError = "";

        // if true, produce grayscale image instead of contours
        static  Boolean grayScale = false;

        // if true, produce alternatingly colored contours
        static Boolean alternatingColorContours = false;

        // if true, will discover min/max heights in data and report to console
        static Boolean reportMinMaxHeights = false;

        // ---- timings ----
        static  Boolean reportTimings = false;

        // -------------------------------------------------------------------------------------
        // needs : error reporting
        static Boolean ParseArgs(string[] args)
        {
            Boolean parsed = true;

            if (args.Length > 0)
            {
                // note : quit upon failure
                for (int argIndex = 0; (argIndex < args.Length) && parsed; ++argIndex)
                {
                    if ('-' == args[argIndex].ElementAt(0))
                    {
                        switch (args[argIndex].ElementAt(1))
                        {
                            case 'o':  // -o : name of output file
                                outputFileName = args[argIndex].Substring(2);
                                break;
                            case 'c': // -c : contour heights
                                {
                                    String  contourHeightsString = args[ argIndex ].Substring(2);
                                    if (false == int.TryParse(contourHeightsString, out contourHeights))
                                    {
                                        parseError = "Could not parse " + contourHeightsString + " as a floating point value.";
                                        parsed = false;
                                    }
                                }
                                break;
                            case 'g' :
                                grayScale = true;
                                break;
                            case 't' :
                                reportTimings = true;
                                break;
                            case 'a' :
                                alternatingColorContours = true;
                                break;
                            case 'm' :
                                reportMinMaxHeights = true;
                                break;
                            case '?' :
                                helpRequested = true;
                                parsed = false;
                                break;
                        }   // end switch
                    }
                    else
                    {
                        // no dash 
                        // requesting help?
                        if ('?' == args[argIndex].ElementAt(0))
                        {
                            parsed = false;
                            helpRequested = true;
                        }
                        else
                        {
                            // input file name (we think/hope)
                            inputFileBaseName = args[argIndex];
                        }
                    }
                }
            }
            else
            {
                parseError = noInputsErrorMessage;
                parsed = false;
            }

            if ( "" == inputFileBaseName )
            {
                parseError = noInputFileSpecifiedErrorMessage;
                parsed = false;
            }

            return parsed;
        }

        // -------------------------------------------------------------------------------------
        static void ListArguments()
        {
            Console.WriteLine( "Program arguments:" );
            Console.WriteLine( "   Required:" );
            Console.WriteLine( "      InputFile : (no prefix dash) Name of input file (without extension, there must be both an FLT and HDR data file with this name)" );
            Console.WriteLine( "   Optional:" );
            Console.WriteLine( "      -? or ? : view available arguments" );
            Console.WriteLine( "      -cNNN : use height division NNN for contour lines (defaults to " + DEFAULT_CONTOUR_HEIGHTS + ")" );
            Console.WriteLine( "      -oMyOutputFile : Sends output to file 'MyOutputFile.bmp'" );
            Console.WriteLine( "      -a : odd/even contours rendered in different colors" );
            Console.WriteLine( "      -g : creates grayscale bmp, black and white are lowest and highest values in height data, respectively." );
            Console.WriteLine( "      -m : discovers min/max heights in data, reports to console" );
            Console.WriteLine( "      -t : report on timing of operations" );
        }

        static  int lowRed = 85;
        static  int lowGreen = 85;
        static  int lowBlue = 85;

        static  int highRed = 255;
        static  int highGreen = 255;
        static  int highBlue = 255;

        static  int redRange = highRed - lowRed;
        static  int greenRange = highGreen - lowGreen;
        static  int blueRange = highBlue - lowBlue;

        static  Int32     NormalizedHeightToColor( float height )
        {
            byte    redValue = (byte)(lowRed + ( height * redRange ) );
            byte    greenValue = (byte)(lowGreen + ( height * greenRange ) );
            byte    blueValue = (byte)(lowBlue + ( height * blueRange ) );

            return  (Int32)(((byte)0xFF << 24) | (redValue << 16) | (greenValue << 8) | blueValue);
        }

        // -------------------------------------------------------------------------------------------------------------------
        static  void    TopoToGrayScaleBitmap( FLTTopoData   topoData, String fileName )
        {
            if ( false == topoData.MinMaxFound )
            {
                topoData.FindMinMax();
            }

            float range = topoData.MaximumElevation - topoData.MinimumElevation;
            float oneOverRange = 1.0f / range;

            Bitmap bmp = new Bitmap(topoData.NumCols(), topoData.NumRows(), System.Drawing.Imaging.PixelFormat.Format32bppRgb);

            Int32[]     pixels = new Int32[ topoData.NumCols() * topoData.NumRows() ];

            // generate grayscale bitmap from normalized topo data
            for (int row = 0; row < topoData.NumRows(); ++row)
            {
                for (int col = 0; col < topoData.NumCols(); ++col)
                //Parallel.For( 0, topoData.NumCols(), col =>
                {
                    float normalizedValue = (topoData.ValueAt(row, col) - topoData.MinimumElevation) * oneOverRange;

                    /*
                    if ( normalizedValue > 0.25f )
                    {
                        int breakpoint = 1;
                    }
                     * */

                    //byte    pixelValue = (byte)(normalizedValue * 255.0f);
                    // grayscale (for now)
                    //Int32 argb = (Int32)(((byte)0xFF << 24) | (pixelValue << 16) | (pixelValue << 8) | pixelValue);

                    //bmp.SetPixel(col, row, Color.FromArgb(argb));
                    pixels[ row * topoData.NumCols() + col ] = NormalizedHeightToColor( normalizedValue );// argb;
                }  //);  // end Parallel.For col
            }   // end for row

            System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                IntPtr pointer = handle.AddrOfPinnedObject();

                //Lock all pixels
                System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(
                           new Rectangle(0, 0, bmp.Width, bmp.Height ),
                           System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);

                //set the beginning of pixel data
                //bmpData.Scan0 = pointer;
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, topoData.NumCols() * topoData.NumRows());

                //Unlock the pixels
                bmp.UnlockBits(bmpData);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

            // write out bitmap
            bmp.Save( fileName + ".bmp");
        }

        // -------------------------------------------------------------------------------------------------------------------
        // note : assumes topoData has been quantized by contourHeights
        static void TopoToContourBitmap(FLTTopoData topoData, String fileName )
        {
            //Create an empty Bitmap of the expected size
            Bitmap contourMap = new Bitmap(topoData.NumCols(), topoData.NumRows(), System.Drawing.Imaging.PixelFormat.Format32bppRgb);

            Int32 whitePixel = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF);
            Int32 blackPixel = (Int32)(((byte)0xFF << 24) | 0);//(pixelValue << 16) | (pixelValue << 8) | pixelValue);

            Int32 redPixel = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0x0 << 8) | 0x0);
            Int32 greenPixel = (Int32)(((byte)0xFF << 24) | (0x0 << 16) | (0xFF << 8) | 0x0);

#if false
            Int32 currentPixel = blackPixel;

            // serial operation
            for ( int row = 1; row < topoData.NumRows(); ++row )
            {
                for ( int col = 1; col < topoData.NumCols(); ++col )
                {
                    float   currentValue = topoData.ValueAt( row, col );
                    float   aboveValue = topoData.ValueAt( row - 1, col );
                    float   leftValue = topoData.ValueAt( row, col - 1 );

                    currentPixel = ( ( currentValue != leftValue ) || ( currentValue != aboveValue ) ) ? blackPixel : whitePixel;

                    contourMap.SetPixel( col, row, Color.FromArgb( currentPixel ) );
                }
            }
#else
            // need to use byte array for pixels
            Int32[]       pixels = new Int32[ topoData.NumRows() * topoData.NumCols() ];

            // ------------------------------
            // single pixel row computation (alternating color of odd/even contours)
            Func<int, int> ComputePixelRowAlternatingColorContours = (row) =>
            {
                float leftValue = topoData.ValueAt(row, 0);
                // index to first pixel in row
                int currentPixelIndex = row * topoData.NumCols();

                Int32 currentPixel = blackPixel;

                for (int col = 0; col < topoData.NumCols(); ++col)
                {
                    float aboveValue = topoData.ValueAt(row - 1, col);
                    float currentValue = topoData.ValueAt(row, col);

                    bool drawCurrent = ((currentValue != leftValue) || (currentValue != aboveValue)) ? true : false;

                    float highestValue = currentValue;

                    if (aboveValue > highestValue)
                    {
                        highestValue = aboveValue;
                    }
                    if (leftValue > highestValue)
                    {
                        highestValue = leftValue;
                    }

                    if (drawCurrent)
                    {
                        currentPixel = blackPixel;

                        Int32 evenOdd = Convert.ToInt32(highestValue / contourHeights % 2);

                        if (evenOdd <= 0)
                        {
                            currentPixel = redPixel;
                        }
                        else
                        {
                            currentPixel = greenPixel;
                        }
                    }
                    else
                    {
                        currentPixel = whitePixel;
                    }

                    pixels[currentPixelIndex] = currentPixel;

                    ++currentPixelIndex;
                    leftValue = currentValue;
                }

                return row;
            };

            // ------------------------------
            // single pixel row computation (all contours same color)
            Func<int, int>ComputePixelRowSingleColorContours = ( row ) =>
            {
                float   leftValue = topoData.ValueAt( row, 0 );
                // index to first pixel in row
                int     currentPixelIndex = row * topoData.NumCols();

                Int32   currentPixel = blackPixel;

                for ( int col = 0; col < topoData.NumCols(); ++col )
                {
                    float   aboveValue = topoData.ValueAt( row - 1, col );
                    float   currentValue = topoData.ValueAt( row, col );

                    currentPixel = ((currentValue != leftValue) || (currentValue != aboveValue)) ? blackPixel : whitePixel;

                    pixels[ currentPixelIndex ] = currentPixel;

                    ++currentPixelIndex;
                    leftValue = currentValue;
                }

                return row;
            };
            // -----------------------------
            System.Console.WriteLine( "Computing pixel rows" );


            // Hmmm, looks like these loops leave row 0 of the bitmap blank. The parallel has to start at row 1
            // because the inner func uses the row-1, so need to brute force row 0 or something.
            if ( alternatingColorContours )
            {
                Parallel.For( 1, topoData.NumRows(), row =>
                {
                    ComputePixelRowAlternatingColorContours( row );
                } );
            }
            else
            {
                Parallel.For( 1, topoData.NumRows(), row =>
                {
                    ComputePixelRowSingleColorContours( row );
                } );
            }

            // ------------------------------
            // create bitmap
            /*
            // straightforward, and slow
            int x;
            int y;
            // init first row to all white (could do detection here too, but will skip for now)
            for ( x = 0; x < topoData.NumCols(); ++x )
            {
                contourMap.SetPixel( x, 0, Color.FromArgb( whitePixel ) );
            }

            for ( y = 0; y < topoData.NumRows(); ++y )
            {
                for ( x = 0; x < topoData.NumCols(); ++x )
                {
                    contourMap.SetPixel( x, y, Color.FromArgb( pixels[ y, x ] ) );
                }
            }
            */

            System.Console.WriteLine("Creating bitmap");

            System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc( pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                IntPtr pointer = handle.AddrOfPinnedObject();

                //Lock all pixels
                System.Drawing.Imaging.BitmapData bmpData = contourMap.LockBits(
                           new Rectangle(0, 0, topoData.NumCols(), topoData.NumRows()),
                           System.Drawing.Imaging.ImageLockMode.WriteOnly, contourMap.PixelFormat);

                // copy pixels into bitmap
                System.Runtime.InteropServices.Marshal.Copy( pixels, 0, bmpData.Scan0, topoData.NumCols() * topoData.NumRows() );

                //Unlock the pixels
                contourMap.UnlockBits(bmpData);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
#endif
            // write out bitmap
            contourMap.Save( fileName + ".bmp" );
        }

        // -------------------------------------------------------------------------------------------------------------------
        static void Main(string[] args)
        {
            FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            long    lastOperationTimingMS = 0;

            System.Console.WriteLine("- - - - - - - - - - -");
            System.Console.WriteLine("FLT Topo Data Contour Generator (run with '?' for available arguments)");

            Boolean parsed = ParseArgs(args);

            if (parsed)
            {
                // if no output file name was specified, create one
                if ( "" == outputFileName )
                {
                    outputFileName = inputFileBaseName + "-Contour";
                }

                System.Console.WriteLine("Input files name : " + inputFileBaseName);
                System.Console.WriteLine("Output file name : " + outputFileName );
                System.Console.WriteLine("Contour heights  : " + contourHeights);
                if (grayScale)
                {
                    System.Console.WriteLine( "Outputting grayscale image." );
                }
                if ( alternatingColorContours )
                {
                    System.Console.WriteLine( "Alternating colors for odd/even contours." );
                }
                if ( reportMinMaxHeights )
                {
                    System.Console.WriteLine( "Reporting min/max heights of data." );
                }
                System.Console.WriteLine( "- - - - - - - - - - -" );

                // ---- read data ----
                try
                {
                    stopwatch.Reset();
                    stopwatch.Start();

                    topoData.ReadFromFiles(inputFileBaseName);

                    stopwatch.Stop();
                }
                catch (System.IO.FileNotFoundException e)  {
                                    Console.WriteLine( "-- ERROR --" );
                                    Console.WriteLine( e.Message );
                                    Console.WriteLine( "\nAn .hdr and .flt data file must exist in the current directory." );
                                    return; //throw; 
                                    }
                catch {
                        throw;
                }


                lastOperationTimingMS = stopwatch.ElapsedMilliseconds;
                if (reportTimings)
                {
                    System.Console.WriteLine("Data read took " + (lastOperationTimingMS / 1000.0f) + " seconds.");
                }

                System.Console.WriteLine("- - - - - - - - - - -");

                // ---- report min max height ----
                if ( reportMinMaxHeights )
                {
                    System.Console.WriteLine( "Finding min/max heights..." );
                    stopwatch.Reset();
                    stopwatch.Start();

                    topoData.FindMinMax();
                    System.Console.WriteLine( "Minimum height found : " + topoData.MinimumElevation );
                    System.Console.WriteLine( "Maximum height found : " + topoData.MaximumElevation );

                    lastOperationTimingMS = stopwatch.ElapsedMilliseconds;
                    if (reportTimings)
                    {
                        System.Console.WriteLine("Min/Max discovery took " + (lastOperationTimingMS / 1000.0f) + " seconds.");
                    }

                    System.Console.WriteLine("- - - - - - - - - - -");
                }   // end if reportMinMaxHeights

                // ---- quantize data ----
                if ( contourHeights > 1 )
                {
                    System.Console.WriteLine( "Quantizing topo data." );

                    stopwatch.Reset();
                    stopwatch.Start();

                    topoData.Quantize( contourHeights );

                    stopwatch.Stop();
                    lastOperationTimingMS = stopwatch.ElapsedMilliseconds;
                    if ( reportTimings )
                    {
                        System.Console.WriteLine( "Quantization took " + (lastOperationTimingMS / 1000.0f ) + " seconds." );
                    }
                }

                // ---- produce output file ----
                System.Console.WriteLine("- - - - - - - - - - -");
                if (grayScale)
                {
                    System.Console.WriteLine( "Creating grayscale bitmap." );

                    stopwatch.Reset();
                    stopwatch.Start();

                    TopoToGrayScaleBitmap(topoData, outputFileName);

                    stopwatch.Stop();
                    lastOperationTimingMS = stopwatch.ElapsedMilliseconds;
                    if (reportTimings)
                    {
                        System.Console.WriteLine("Grayscale creation took " + (lastOperationTimingMS / 1000.0f) + " seconds.");
                        // took 145 seconds with 'large' grid
                    }
                }
                else
                {
                    // make contour map
                    System.Console.WriteLine( "Creating contour map." );

                    stopwatch.Reset();
                    stopwatch.Start();

                    TopoToContourBitmap( topoData, outputFileName );

                    stopwatch.Stop();
                    lastOperationTimingMS = stopwatch.ElapsedMilliseconds;
                    if (reportTimings)
                    {
                        System.Console.WriteLine("Contour map creation took " + (lastOperationTimingMS / 1000.0f) + " seconds.");
                    }

                }
            }
            else
            {
                if ( helpRequested )
                {
                    ListArguments();
                }
                else
                {
                    System.Console.WriteLine("Input error : " + parseError);

                    if ( noInputsErrorMessage == parseError )
                    {
                        ListArguments();
                    }
                }
            }

            System.Console.WriteLine("- - - - - - - - - - -\n");
        }
    }
}

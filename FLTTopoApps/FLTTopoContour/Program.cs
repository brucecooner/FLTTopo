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
            Console.WriteLine( "      -g : creates grayscale bmp, black and white are lowest and highest values in height data, respectively." );
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
            // single pixel row computation 
            Func<int, int>ComputePixelRow = ( row ) =>
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

            // Hmmm, looks like this leaves row 0 of the bitmap blank. The parallel has to start at row 1
            // because the inner func uses the row above, so need to brute force row 0 or something.
            Parallel.For( 1, topoData.NumRows(), row =>
            {
                ComputePixelRow( row );
            }
            );

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
                System.Console.WriteLine( "- - - - - - - - - - -" );

                // ---- test for file existence ----
                /*
                // TODO : this sort of specialized file knowledge should be in the ftl library
                if ( !File.Exists( inputFileBaseName + ".hdr" ) )
                {
                    Console.WriteLine( "Error : Input file " + inputFileBaseName + ".hdr not found." );
                }

                if (!File.Exists(inputFileBaseName + ".flt"))
                {
                    Console.WriteLine("Error : Input file " + inputFileBaseName + ".flt not found.");
                }
                 */

                // ---- read data ----
                try
                {
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

                // ---- quantize data ----
                if ( contourHeights > 1 )
                {
                    System.Console.WriteLine( "Quantizing topo data." );

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

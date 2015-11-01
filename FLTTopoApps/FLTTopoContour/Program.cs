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
 *  -file overwrite confirmation
    -'slice' mode
    -subgrid processing
    -configurable colors
    -config file
 *  -suppress output?
 * */

namespace FLTTopoContour
{
    class Program
    {
        const float versionNumber = 1.1f;

        // TYPES
        // different options the user specifies
        enum OptionType
        {
            HelpRequest,
            ReportTimings,
            Mode,
            OutputFile,
            ContourHeights
        };

        // different types of contour maps the app can produce
        enum OutputModeType
        {
            Normal          = 'n',    // regular-like topo map, contour lines every contourHeights feet
            Alternating     = 'a',    // alternating contour lines are made in alternating colors
            Gradient        = 'g'     // image rendered with color gradient from lowest point (default:black) to highest point (default:white) at contourHeights steps
        }

        delegate Boolean ParseOptionDelegate( String input, ref String parseErrorString );

        class OptionSpecifier
        {
            public String   Specifier;          // string that triggers option
            public String   HelpText;           // shown in help mode
            public String   Description;        // used when reporting option

            public ParseOptionDelegate ParseDelegate;   // used to translate input text to option value

            // list of (string type only) parameters that can specify values for this parameter
            public List<OptionSpecifier> AllowedValues;

            public OptionSpecifier()
            {
                Specifier = "";
                HelpText = "";
                ParseDelegate = null;
                AllowedValues = null;
            }
        };

        // TODO : settle on a capitalization scheme here

        // CONSTANTS
        const String noInputsErrorMessage = "No inputs.";
        const String noInputFileSpecifiedErrorMessage = "No input file specified.";
        const String ConsoleSectionSeparator = "- - - - - - - - - - -";
        const String BannerMessage = "FLT Topo Data Contour Generator (run with '?' for options list)";  // "You wouldn't like me when I'm angry."
        const char HelpRequestChar = '?';

        const Int32 MinimumContourHeights = 5;

        // DEFAULTS
        const String DefaultOutputFileSuffix = "_topo";
        const int DefaultContourHeights = 200;
        const OutputModeType DefaultOutputMode = OutputModeType.Normal;

        // OPTIONS
        // supported options
        static Dictionary< OptionType, OptionSpecifier > optionTypeToSpecDict;

        // maps output modes onto their specifiers
        static Dictionary< OutputModeType, OptionSpecifier > outputModeToSpecifierDict;

        static String inputFileBaseName = "";
        static String outputFileName = "";

        static bool helpRequested = false;

        static OutputModeType outputMode = DefaultOutputMode;

        static Int32 contourHeights = DefaultContourHeights;

        static  Boolean reportTimings = false;

        static String parseErrorMessage = "";

        // ---- parse delegates ----
        // ------------------------------------------------------
        static private Boolean ParseOutputFile( String input, ref String parseErrorString )
        {
            Boolean parsed = false;

            if ( input.Length > 0 )
            {
                outputFileName = input;
                parsed = true;
            }
            else
            {
                parsed = false;
                parseErrorString = "Output file name was empty";
            }

            return parsed;
        }

        // ------------------------------------------------------
        static private Boolean ParseContourHeights( String input, ref String parseErrorString )
        {
            Boolean parsed = false;

            parsed = Int32.TryParse( input, out contourHeights );

            if ( false == parsed )
            {
                parseErrorString = "Specified contour height '" + input + "' could not be converted to a number.";
            }
            else
            {
                if ( contourHeights < MinimumContourHeights )
                {
                    parsed = false;
                    parseErrorMessage = "Minimum allowed contour height is " + MinimumContourHeights;
                }
            }

            return parsed;
        }

        // ------------------------------------------------------
        static private Boolean ParseMode( String input, ref String parseErrorString )
        {
            Boolean parsed = false;

            foreach ( KeyValuePair< OutputModeType, OptionSpecifier > currentModeSpec in outputModeToSpecifierDict )
            {
                if ( input.Equals( currentModeSpec.Value.Specifier ) )
                {
                    outputMode = currentModeSpec.Key;
                    parsed = true;
                }
            }

            if ( false == parsed )
            {
                parseErrorString = "Specified mode '" + input + "' not recognized.";
            }

            return parsed;
        }

        // ------------------------------------------------
        static private Boolean ParseReportTimings( String input, ref String parseErrorString )
        {
            reportTimings = true;   // if option is used, it's turned on
            return true;
        }

        // ------------------------------------------------
        static private Boolean ParseHelpRequest(String input, ref String parseErrorString)
        {
            helpRequested = true;   // if option is used, it's turned on
            return true;
        }

        // ---- static constructor ----
        static public void InitOptionSpecifiers()
        {
            outputModeToSpecifierDict = new Dictionary< OutputModeType, OptionSpecifier>( Enum.GetNames(typeof(OutputModeType)).Length );

            // not crazy about these casts, will see how it works in practice
            // TODO : more descriptive help text?
            outputModeToSpecifierDict.Add( OutputModeType.Normal,        new OptionSpecifier {  Specifier = ((char)OutputModeType.Normal).ToString(),      
                                                                                                Description = "Normal",
                                                                                                HelpText = "Normal contour map" });
            outputModeToSpecifierDict.Add( OutputModeType.Gradient,      new OptionSpecifier {  Specifier = ((char)OutputModeType.Gradient).ToString(),    
                                                                                                Description = "Gradient",
                                                                                                HelpText = "Gradient" });
            outputModeToSpecifierDict.Add( OutputModeType.Alternating,   new OptionSpecifier {  Specifier = ((char)OutputModeType.Alternating).ToString(), 
                                                                                                Description = "Alternating",
                                                                                                HelpText = "Alternating colored contour lines" } );

            optionTypeToSpecDict = new Dictionary< OptionType, OptionSpecifier>( Enum.GetNames(typeof(OptionType)).Length );

            optionTypeToSpecDict.Add( OptionType.HelpRequest,   new OptionSpecifier{    Specifier = HelpRequestChar.ToString(), 
                                                                                        Description = "Help (list available options)",
                                                                                        HelpText = ": print all available parameters",
                                                                                        ParseDelegate = ParseHelpRequest } );
            optionTypeToSpecDict.Add( OptionType.ReportTimings, new OptionSpecifier{    Specifier = "t", 
                                                                                        Description = "Report Timings",
                                                                                        HelpText = ": report Timings",
                                                                                        ParseDelegate = ParseReportTimings });
                                                                                        
            optionTypeToSpecDict.Add( OptionType.Mode,          new OptionSpecifier{    Specifier = "m", 
                                                                                        Description = "Output mode",
                                                                                        HelpText = "<M>: mode (type of output)", 
                                                                                        AllowedValues = outputModeToSpecifierDict.Values.ToList<OptionSpecifier>(), 
                                                                                        ParseDelegate = ParseMode });
            optionTypeToSpecDict.Add( OptionType.OutputFile,    new OptionSpecifier{    Specifier = "o", 
                                                                                        Description = "Output file",
                                                                                        HelpText = "<OutputFile>: specifies output image file",
                                                                                        ParseDelegate = ParseOutputFile });
            optionTypeToSpecDict.Add( OptionType.ContourHeights,new OptionSpecifier{    Specifier = "c", 
                                                                                        Description = "Contour heights",
                                                                                        HelpText = "<NNN>: Contour height separation (every NNN units), Minimum allowed value : " + MinimumContourHeights,
                                                                                        ParseDelegate = ParseContourHeights } );
        }

        // --------------------------------------------------------------------------------------
        static void ListAvailableOptions()
        {
            // ugh, probably a friendlier way to do this
            const String indent = "   ";
            const String indent2 = indent + indent;
            const String indent3 = indent2 + indent;
            const String indent4 = indent2 + indent2;

            Console.WriteLine( ConsoleSectionSeparator );
            Console.WriteLine( "Options:" );
            Console.WriteLine( indent + "Required:" );
            Console.WriteLine( indent2 + "InputFile : (no prefix dash) Name of input file (without extension, there must be both an FLT and HDR data file with this name)" );

            Console.WriteLine( indent + "Optional:" );
            foreach ( var currentOptionSpec in optionTypeToSpecDict.Values )
            {
                // Note : hardwiring the dash in front of these optional parameters
                // also note that description string immediately follows specifier
                String currentParamString = indent2 + "-" + currentOptionSpec.Specifier + currentOptionSpec.HelpText;

                Console.WriteLine(currentParamString);

                if ( null != currentOptionSpec.AllowedValues )
                {
                    Console.WriteLine( indent3 + "Options : " );
                    foreach ( var currentAllowedValueSpec in currentOptionSpec.AllowedValues )
                    {
                        String currentAllowedValueString  = indent4 + currentAllowedValueSpec.Specifier + " : " + currentAllowedValueSpec.HelpText;
                        Console.WriteLine( currentAllowedValueString );
                    }
                }
            }
        }

        // -------------------------------------------------------------------------------------
        static Boolean ParseArgs(string[] args)
        {
            Boolean parsed = true;

            if ( 0 == args.Length )
            {
                parseErrorMessage = noInputsErrorMessage;
                helpRequested = true;
                parsed = false;
            }
            else
            {
                foreach ( string currentArg in args )
                {
                    if ( '-' == currentArg.ElementAt(0))
                    {
                        String currentOptionString = currentArg.Substring(1); // skip dash
                        Boolean matched = false;

                        // try to match an option specifier
                        foreach ( var currentOptionSpec in optionTypeToSpecDict.Values )
                        {
                            if (currentOptionString.StartsWith(currentOptionSpec.Specifier))
                            {
                                matched = true;

                                // skip specifier string
                                currentOptionString = currentOptionString.Substring( currentOptionSpec.Specifier.Length );

                                parsed = currentOptionSpec.ParseDelegate( currentOptionString, ref parseErrorMessage );
                            }

                            if ( matched )
                            {
                                break; 
                            }
                        }   // end foreach option spec

                        // detect invalid options
                        if ( false == matched )
                        {
                            parseErrorMessage = "Unrecognized option : " + currentArg;
                            parsed = false;
                        }
                    }
                    else // no dash
                    {
                        // I'll specifically support no dash on ye olde help request
                        // requesting help?
                        if ( HelpRequestChar == currentArg.ElementAt(0))
                        {
                            helpRequested = true;
                        }
                        else
                        {
                            // input file name (we think/hope)
                            inputFileBaseName = currentArg;
                        }
                    }   // end else

                    // if current parse failed get out
                    if ( false == parsed )
                    {
                        break;
                    }
                }
            }

            // must specify input file
            if ( 0 == inputFileBaseName.Length )
            {
                parseErrorMessage = noInputFileSpecifiedErrorMessage;
                parsed = false;
            }

            // default output file name to input file name plus something extra
            if ( 0 == outputFileName.Length )
            {
                outputFileName = inputFileBaseName + DefaultOutputFileSuffix;
            }

            return parsed;
        }

        // -------------------------------------------------------------------------------------
        static public void ReportOptionValues()
        {
            Console.WriteLine(ConsoleSectionSeparator);

            // TODO : automate?
            Console.WriteLine("Input file base name : " + inputFileBaseName);
            Console.WriteLine(optionTypeToSpecDict[OptionType.OutputFile].Description + " : " + outputFileName);
            Console.WriteLine(optionTypeToSpecDict[OptionType.Mode].Description + " : " + outputModeToSpecifierDict[outputMode].Description);
            Console.WriteLine(optionTypeToSpecDict[OptionType.ContourHeights].Description + " : " + contourHeights);
            Console.WriteLine(optionTypeToSpecDict[OptionType.ReportTimings].Description + " : " + (reportTimings ? "yes" : "no"));
        }

        // TODO : move to where they should live
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
            if ( OutputModeType.Alternating == outputMode )
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
        // -------------------------------------------------------------------------------------------------------------------
        static void Main(string[] args)
        {
            // ----- startup -----
            InitOptionSpecifiers();

            FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            long    lastOperationTimingMS = 0;

            System.Console.WriteLine();
            System.Console.WriteLine( BannerMessage );
            System.Console.WriteLine( "Version : " + versionNumber.ToString() );

            // ----- parse program arguments -----
#if true
            Boolean parsed = ParseArgs(args);
#else
            // testing
            string[] testArgs = new string[2];
            testArgs[0] = "inputTsetFile";
            testArgs[1] = "-mn";
            Boolean parsed = ParseArgs( testArgs );
#endif

            if ( false == parsed )
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Error : " + parseErrorMessage);

                if ( helpRequested )
                {
                    ListAvailableOptions();
                }
            }
            else // args parsed successfully
            {
                if (helpRequested)
                {
                    ListAvailableOptions();
                }

                // report current options
                ReportOptionValues();

                // TODO : break into operations

                System.Console.WriteLine( ConsoleSectionSeparator );

                // ---- read data ----
                try
                {
                    stopwatch.Reset();
                    stopwatch.Start();

                    topoData.ReadFromFiles(inputFileBaseName);

                    stopwatch.Stop();
                }
                catch (System.IO.FileNotFoundException e)  
                {
                    Console.WriteLine();
                    Console.WriteLine( "Error:" );
                    Console.WriteLine( e.Message );
                    Console.WriteLine( "\nAn .hdr and .flt data file must exist in the current directory." );
                    return; //throw; 
                }
                catch 
                {
                    throw;
                }


                lastOperationTimingMS = stopwatch.ElapsedMilliseconds;
                if (reportTimings)
                {
                    System.Console.WriteLine("Data read took " + (lastOperationTimingMS / 1000.0f) + " seconds.");
                }

                System.Console.WriteLine( ConsoleSectionSeparator );

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
                System.Console.WriteLine( ConsoleSectionSeparator );
                if ( OutputModeType.Gradient == outputMode )
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

            System.Console.WriteLine();
        }
    }
}

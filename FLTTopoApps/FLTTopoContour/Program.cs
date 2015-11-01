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
    -config file?
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
            HelpRequest,        // list options
            ReportTimings,      // report how long various operations took
            Mode,               // output topo map type
            OutputFile,         // name of output file
            ContourHeights,     // vertical distance between contours
            BackgroundColor,    // color of 'ground' between contour lines
            ContourColor,       // color of contour lines in normal mode
            AlternatingColor1,  // color of half the lines in alternating mode
            AlternatingColor2,  // color of other half of lines in alternating mode
            GradientLoColor,    // color at lowest point in gradient mode
            GradientHiColor     // color at highest point in gradient mode
        };

        // different types of contour maps the app can produce
        enum OutputModeType
        {
            Normal          = 'n',    // regular-like topo map, contour lines every contourHeights feet
            Alternating     = 'a',    // alternating contour lines are made in alternating colors
            Gradient        = 'g'     // image rendered with color gradient from lowest point (Color1) to highest point (Color2) at contourHeights steps
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
        const String ColorsHelpMessage = "Note : Colors are specified as a 'hex triplet' of the form RRGGBB\nwhere RR = red value, GG = green value, and BB = blue value.\nThe values are given in base-16.";

        const Int32 MinimumContourHeights = 5;

        // DEFAULTS
        const String DefaultOutputFileSuffix = "_topo";
        const int DefaultContourHeights = 200;
        const OutputModeType DefaultOutputMode = OutputModeType.Normal;

        const String DefaultBackgroundColorString = "FFFFFF";
        const Int32 DefaultBackgroundColor = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF); // white

        const String DefaultContourColorString = "000000";
        const Int32 DefaultContourColor = (Int32)(((byte)0xFF << 24) | 0); // black

        const String DefaultAlternatingColor1String = "FF0000";
        const Int32 DefaultAlternatingColor1 = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0x0 << 8) | 0x0); // red

        const String DefaultAlternatingColor2String = "00FF00";
        const Int32 DefaultAlternatingColor2 = (Int32)(((byte)0xFF << 24) | (0x0 << 16) | (0xFF << 8) | 0x0); // green

        const String DefaultGradientLoColorString = "000000";
        const Int32 DefaultGradientLoColor = (Int32)(((byte)0xFF << 24) | (0x55 << 16) | (0x55 << 8) | 0x55); // gray-ish

        const String DefaultGradientHiColorString = "000000";
        const Int32 DefaultGradientHiColor = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF); // white

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

        // background color (used in normal and alternating modes)
        static Int32 backgroundColor = DefaultBackgroundColor;
        static String backgroundColorString = DefaultBackgroundColorString;

        // normal mode 
        static Int32 contourColor = DefaultContourColor;
        static String contourColorString = DefaultContourColorString;

        // alternating mode 
        static Int32 alternatingContourColor1 = DefaultAlternatingColor1;
        static String alternatingContourColor1String = DefaultAlternatingColor1String;
        static Int32 alternatingContourColor2 = DefaultAlternatingColor2;
        static String alternatingContourColor2String = DefaultAlternatingColor2String;

        // gradient mode 
        static Int32 gradientLoColor = DefaultGradientLoColor;
        static String gradientLoColorString = DefaultGradientLoColorString;
        static Int32 gradientHiColor = DefaultGradientHiColor;
        static String gradientHiColorString = DefaultGradientHiColorString;

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
        // converts hex triplet to 32 bit pixel (note : does NOT expect 0x prefix on string)
        static private Int32 ParseColorHexTriplet( String input, ref Boolean parsed, ref String parseErrorString )
        {
            parsed = true;
            Int32 colorValue = 0;

            if (6 != input.Length)
            {
                parsed = false;
                parseErrorString = "color string not six digits in length";
            }
            else
            {
                String redString = input.Substring( 0, 2 );
                String greenString = input.Substring( 2, 2 );
                String blueString = input.Substring( 4, 2 );

                Int32 redValue = 0;
                Int32 greenValue = 0;
                Int32 blueValue = 0; 

                try
                {
                    redValue = Convert.ToInt32( redString, 16 );
                }
                catch ( System.FormatException )
                {
                    parsed = false;
                    parseErrorString = "could not convert '" + redString + "' to red value";
                }

                try
                {
                    greenValue = Convert.ToInt32( greenString, 16 );
                    blueValue = Convert.ToInt32( blueString, 16 );
                }
                catch (System.FormatException)
                {
                    parsed = false;
                    parseErrorString = "could not convert '" + greenString + "' to green value";
                }

                try
                {
                    blueValue = Convert.ToInt32(blueString, 16);
                }
                catch (System.FormatException)
                {
                    parsed = false;
                    parseErrorString = "could not convert '" + blueString + "' to blue value";
                }

                colorValue = (Int32)(((byte)0xFF << 24) | (redValue << 16) | (greenValue << 8) | blueValue);
            }

            return colorValue;
        }

        // ------------------------------------------------------
        // generalized color parsing
        static private Boolean ParseColor( String input, String colorDescription, ref String parseErrorString, ref Int32 colorOut, ref String colorStringOut )
        {
            Boolean parsed = true;

            String parseError = "";
            colorOut = ParseColorHexTriplet(input, ref parsed, ref parseError);

            if (false == parsed)
            {
                parseErrorString = "Converting '" + input + "' to " + colorDescription + ", " + parseError;
            }
            else
            {
                colorStringOut = input;
            } 

            return parsed;
        }

        // ------------------------------------------------------
        static private Boolean ParseBackgroundColor( String input, ref String parseErrorString )
        {
            return ParseColor( input, optionTypeToSpecDict[ OptionType.BackgroundColor ].Description, ref parseErrorString, ref backgroundColor, ref backgroundColorString );
        }

        // ------------------------------------------------------
        static private Boolean ParseContourColor(String input, ref String parseErrorString)
        {
            return ParseColor( input, optionTypeToSpecDict[ OptionType.ContourColor ].Description, ref parseErrorString, ref contourColor, ref contourColorString );
        }

        // ------------------------------------------------------
        static private Boolean ParseAlternatingContourColor1( String input, ref String parseErrorString)
        {
            return ParseColor( input, optionTypeToSpecDict[ OptionType.AlternatingColor1 ].Description, ref parseErrorString, ref alternatingContourColor1, ref alternatingContourColor1String );
        }

        // ------------------------------------------------------
        static private Boolean ParseAlternatingContourColor2(String input, ref String parseErrorString)
        {
            return ParseColor( input, optionTypeToSpecDict[ OptionType.AlternatingColor2 ].Description, ref parseErrorString, ref alternatingContourColor2, ref alternatingContourColor2String );
        }

        // ------------------------------------------------------
        static private Boolean ParseGradientLoColor(String input, ref String parseErrorString)
        {
            return ParseColor( input, optionTypeToSpecDict[ OptionType.GradientLoColor ].Description, ref parseErrorString, ref gradientLoColor, ref gradientLoColorString );
        }

        // ---------------------------------------------------------
        static private Boolean ParseGradientHiColor( String input, ref String parseErrorString )
        {
            return ParseColor( input, optionTypeToSpecDict[ OptionType.GradientHiColor ].Description, ref parseErrorString, ref gradientHiColor, ref gradientHiColorString );
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
                                                                                                HelpText = "Gradient of colors between lowest and highest elevations on map" });
            outputModeToSpecifierDict.Add( OutputModeType.Alternating,   new OptionSpecifier {  Specifier = ((char)OutputModeType.Alternating).ToString(), 
                                                                                                Description = "Alternating",
                                                                                                HelpText = "Contour line colors alternate between altcolor1 and altcolor2" } );

            optionTypeToSpecDict = new Dictionary< OptionType, OptionSpecifier>( Enum.GetNames(typeof(OptionType)).Length );

            optionTypeToSpecDict.Add( OptionType.HelpRequest,   new OptionSpecifier{    Specifier = HelpRequestChar.ToString(), 
                                                                                        Description = "Help (list available options)",
                                                                                        HelpText = ": print all available parameters",
                                                                                        ParseDelegate = ParseHelpRequest } );
            optionTypeToSpecDict.Add( OptionType.ReportTimings, new OptionSpecifier{    Specifier = "timings", 
                                                                                        Description = "Report Timings",
                                                                                        HelpText = ": report Timings",
                                                                                        ParseDelegate = ParseReportTimings });
                                                                                        
            optionTypeToSpecDict.Add( OptionType.Mode,          new OptionSpecifier{    Specifier = "mode", 
                                                                                        Description = "Output mode",
                                                                                        HelpText = "<M>: mode (type of output)", 
                                                                                        AllowedValues = outputModeToSpecifierDict.Values.ToList<OptionSpecifier>(), 
                                                                                        ParseDelegate = ParseMode });
            optionTypeToSpecDict.Add( OptionType.OutputFile,    new OptionSpecifier{    Specifier = "outfile", 
                                                                                        Description = "Output file",
                                                                                        HelpText = "<OutputFile>: specifies output image file",
                                                                                        ParseDelegate = ParseOutputFile });
            optionTypeToSpecDict.Add( OptionType.ContourHeights,new OptionSpecifier{    Specifier = "cont", 
                                                                                        Description = "Contour heights",
                                                                                        HelpText = "<NNN>: Contour height separation (every NNN units), Minimum : " + MinimumContourHeights,
                                                                                        ParseDelegate = ParseContourHeights } );
            optionTypeToSpecDict.Add(OptionType.BackgroundColor,new OptionSpecifier{    Specifier = "bgcolor",
                                                                                        Description = "Background color",
                                                                                        HelpText = "<RRGGBB>: Background Color",
                                                                                        ParseDelegate = ParseBackgroundColor });
            optionTypeToSpecDict.Add(OptionType.ContourColor, new OptionSpecifier{     Specifier = "concolor",
                                                                                        Description = "Contour lines color",
                                                                                        HelpText = "<RRGGBB>: color of contour lines in normal mode",
                                                                                        ParseDelegate = ParseContourColor });
            optionTypeToSpecDict.Add(OptionType.AlternatingColor1, new OptionSpecifier{ Specifier = "altcolor1",
                                                                                        Description = "Alternating contour colors 1",
                                                                                        HelpText = "<RRGGBB>: color 1 in alternating mode",
                                                                                        ParseDelegate = ParseAlternatingContourColor1 });
            optionTypeToSpecDict.Add(OptionType.AlternatingColor2, new OptionSpecifier{ Specifier = "altcolor2",
                                                                                        Description = "Alternating contour colors 2",
                                                                                        HelpText = "<RRGGBB>: color 2 in alternating mode",
                                                                                        ParseDelegate = ParseAlternatingContourColor2 });
            optionTypeToSpecDict.Add(OptionType.GradientLoColor, new OptionSpecifier{   Specifier = "gradlocolor",
                                                                                        Description = "Gradient mode low point color",
                                                                                        HelpText = "<RRGGBB> color of lowest points on map in gradient mode",
                                                                                        ParseDelegate = ParseGradientLoColor });
            optionTypeToSpecDict.Add(OptionType.GradientHiColor, new OptionSpecifier{   Specifier = "gradhicolor",
                                                                                        Description = "Gradient mode high point color",
                                                                                        HelpText = "<RRGGBB> color of highest points on map in gradient mode",
                                                                                        ParseDelegate = ParseGradientHiColor
            });

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
            Console.WriteLine( ColorsHelpMessage );
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
            // only report colors if changed from default
            if ( backgroundColor != DefaultBackgroundColor )
            {
                Console.WriteLine( optionTypeToSpecDict[OptionType.BackgroundColor].Description + " : " + backgroundColorString );
            }
            if ( contourColor != DefaultContourColor )
            {
                Console.WriteLine(optionTypeToSpecDict[OptionType.ContourColor].Description + " : " + contourColorString);
            }
            if ( alternatingContourColor1 != DefaultAlternatingColor1 )
            {
                Console.WriteLine(optionTypeToSpecDict[OptionType.AlternatingColor1].Description + " : " + alternatingContourColor1String);
            }
            if (alternatingContourColor2 != DefaultAlternatingColor2)
            {
                Console.WriteLine(optionTypeToSpecDict[OptionType.AlternatingColor2].Description + " : " + alternatingContourColor2String);
            }
            if (gradientLoColor != DefaultGradientLoColor)
            {
                Console.WriteLine(optionTypeToSpecDict[OptionType.GradientLoColor].Description + " : " + gradientLoColorString);
            }
            if (gradientHiColor != DefaultGradientHiColor)
            {
                Console.WriteLine(optionTypeToSpecDict[OptionType.GradientHiColor].Description + " : " + gradientHiColorString);
            }
        }

        static  int lowRed;
        static  int lowGreen;
        static  int lowBlue;

        static  int highRed;
        static  int highGreen;
        static  int highBlue;

        static  int redRange;
        static  int greenRange;
        static  int blueRange;

        static  Int32     NormalizedHeightToColor( float height )
        {
            byte    redValue = (byte)(lowRed + ( height * redRange ) );
            byte    greenValue = (byte)(lowGreen + ( height * greenRange ) );
            byte    blueValue = (byte)(lowBlue + ( height * blueRange ) );

            return  (Int32)(((byte)0xFF << 24) | (redValue << 16) | (greenValue << 8) | blueValue);
        }

        // -------------------------------------------------------------------------------------------------------------------
        static  void    TopoToGradientBitmap( FLTTopoData   topoData, String fileName )
        {
            // get gradient colors
            lowRed = (gradientLoColor >> 16) & 0xFF;
            lowGreen = (gradientLoColor >> 8) & 0xFF;
            lowBlue = gradientLoColor & 0xFF;

            highRed = (gradientHiColor >> 16) & 0xFF;
            highGreen = (gradientHiColor >> 8) & 0xFF;
            highBlue = gradientHiColor & 0xFF;

            redRange = highRed - lowRed;
            greenRange = highGreen - lowGreen;
            blueRange = highBlue - lowBlue;

            if ( false == topoData.MinMaxFound )
            {
                topoData.FindMinMax();
            }

            float range = topoData.MaximumElevation - topoData.MinimumElevation;
            float oneOverRange = 1.0f / range;

            Bitmap bmp = new Bitmap(topoData.NumCols(), topoData.NumRows(), System.Drawing.Imaging.PixelFormat.Format32bppRgb);

            Int32[]     pixels = new Int32[ topoData.NumCols() * topoData.NumRows() ];

            // generate grayscale bitmap from normalized topo data
            //for (int row = 0; row < topoData.NumRows(); ++row)
            Parallel.For ( 0, topoData.NumRows(), row =>
            {
                int offset = row * topoData.NumCols();

                for (int col = 0; col < topoData.NumCols(); ++col)
                {
                    float normalizedValue = (topoData.ValueAt(row, col) - topoData.MinimumElevation) * oneOverRange;

                    //byte    pixelValue = (byte)(normalizedValue * 255.0f);
                    // grayscale (for now)
                    //Int32 argb = (Int32)(((byte)0xFF << 24) | (pixelValue << 16) | (pixelValue << 8) | pixelValue);

                    //bmp.SetPixel(col, row, Color.FromArgb(argb));
                    //pixels[ row * topoData.NumCols() + col ] = NormalizedHeightToColor( normalizedValue );// argb;
                    pixels[ offset ] = NormalizedHeightToColor(normalizedValue);// argb;
                    ++offset;
                }  
            //}   // end for row
            } );    // end parallel.for row

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

            //Int32 redPixel = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0x0 << 8) | 0x0);
            //Int32 greenPixel = (Int32)(((byte)0xFF << 24) | (0x0 << 16) | (0xFF << 8) | 0x0);

#if false
            // normal, slow way
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

                Int32 currentPixel = backgroundColor;

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
                        currentPixel = backgroundColor;

                        Int32 evenOdd = Convert.ToInt32(highestValue / contourHeights % 2);

                        if (evenOdd <= 0)
                        {
                            currentPixel = alternatingContourColor1;
                        }
                        else
                        {
                            currentPixel = alternatingContourColor2;
                        }
                    }
                    else
                    {
                        currentPixel = backgroundColor; 
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

                Int32   currentPixel = backgroundColor;

                for ( int col = 0; col < topoData.NumCols(); ++col )
                {
                    float   aboveValue = topoData.ValueAt( row - 1, col );
                    float   currentValue = topoData.ValueAt( row, col );

                    currentPixel = ((currentValue != leftValue) || (currentValue != aboveValue)) ? contourColor : backgroundColor;

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
            Boolean parsed = ParseArgs(args);

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
                    System.Console.WriteLine( "Creating bitmap." );

                    stopwatch.Reset();
                    stopwatch.Start();

                    TopoToGradientBitmap(topoData, outputFileName);

                    stopwatch.Stop();
                    lastOperationTimingMS = stopwatch.ElapsedMilliseconds;
                    if (reportTimings)
                    {
                        System.Console.WriteLine("Creation took " + (lastOperationTimingMS / 1000.0f) + " seconds.");
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


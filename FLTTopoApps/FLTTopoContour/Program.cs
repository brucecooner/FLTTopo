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
 *  -file overwrite avoidance or confirmation
    -'slice' modes (horizontal and vertical)
    -config file?
    -suppress output option?
    -is my capitalization all over the place?
    -add program note explaining that top must be < bottom, not greater, and why!
    -return error codes from Main?
    - add 'mark coordinates' option ?
 * */

namespace FLTTopoContour
{
    class Program
    {
        const float versionNumber = 1.4f;

        // TYPES
        // different options the user specifies
        enum OptionType
        {
            HelpRequest,        // list options
            ReportTimings,      // report how long various operations took
            DataReportOnly,     // instead of outputting data, reports on details of topo data
            Mode,               // output topo map type
            OutputFile,         // name of output file
            ContourHeights,     // vertical distance between contours
            BackgroundColor,    // color of 'ground' between contour lines
            ContourColor,       // color of contour lines in normal mode
            AlternatingColor1,  // color of half the lines in alternating mode
            AlternatingColor2,  // color of other half of lines in alternating mode
            GradientLoColor,    // color at lowest point in gradient mode
            GradientHiColor,    // color at highest point in gradient mode
            RectIndices,        // specifies rectangle within topo data to process/output by indices into the grid of points
            RectCoords          // specifies rectangle within topo data to process/output by (floating point) latitude and longitude coordinates 
        };

        // different types of contour maps the app can produce
        enum OutputModeType
        {
            Normal              = 'n',    // regular-like topo map, contour lines every contourHeights feet
            Alternating         = 'a',    // alternating contour lines are made in alternating colors
            Gradient            = 'g',    // image rendered with color gradient from lowest point (Color1) to highest point (Color2) at contourHeights steps
            //SeparateMaps        = 'h'     // normal mode, but each contour height gets its own bitmap
        }

        delegate Boolean ParseOptionDelegate( String input, ref String parseErrorString );

        class OptionSpecifier
        {
            public String   Specifier;          // string, entered in arguments, that denotes option
            public String   HelpText;           // shown in help mode
            public String   Description;        // used when reporting option values

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

        // TODO : settle on a capitalization scheme here!!!

        // CONSTANTS
        const String noInputsErrorMessage = "No inputs.";
        const String noInputFileSpecifiedErrorMessage = "No input file specified.";
        const String ConsoleSectionSeparator = "- - - - - - - - - - -";
        const String BannerMessage = "FLT Topo Data Contour Generator (run with '?' for options list)";  // "You wouldn't like me when I'm angry."
        const char HelpRequestChar = '?';
        const String rectTopName = "top";
        const String rectLeftName = "left";
        const String rectRightName = "right";
        const String rectBottomName = "bottom";
        const String NorthString = "north";
        const String EastString = "east";
        const String SouthString = "south";
        const String WestString = "west";
        const char NorthChar = 'N';
        const char EastChar = 'E';
        const char SouthChar = 'S';
        const char WestChar = 'W';

        const Int32 MinimumContourHeights = 1;

        const float CoordinateNotSpecifiedValue = float.MaxValue;

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

        // ---- SETTINGS ----
        static String inputFileBaseName = "";
        static String outputFileName = "";

        static bool helpRequested = false;

        static OutputModeType outputMode = DefaultOutputMode;

        static int contourHeights = DefaultContourHeights;

        static  Boolean reportTimings = false;

        static Boolean dataReportOnly = false;

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

        static Boolean rectCoordinatesSpecified = false;
        static float rectNorthLatitude;
        static float rectWestLongitude;
        static float rectSouthLatitude;
        static float rectEastLongitude;

        // output sub-rect 
        static Boolean rectIndicesSpecified = false;
        static Int32 rectTopIndex;
        static Int32 rectLeftIndex;
        static Int32 rectRightIndex;
        static Int32 rectBottomIndex;

        // list of single line operating notes
        static List<String> programNotes = null;

        // ---- timing ----
        // timing logs
        static List<Tuple<String, float>> timingLog = new List<Tuple<String, float>>(20);

        static void addTiming( String timingEntryName, float timingEntryValueMS )
        { timingLog.Add( Tuple.Create<String,float>( timingEntryName, timingEntryValueMS / 1000.0f ) ); }

        static void PrintTimings()
        {
            String indent = "  ";

            Console.WriteLine( ConsoleSectionSeparator );
            Console.WriteLine( "Timings:" );
            foreach( var entry in timingLog )
            {
                Console.WriteLine( indent + entry.Item1 + " took " + entry.Item2 + " seconds." );
            }
        }

        // ---- parse delegates ----
        // ------------------------------------------------------
        static private Boolean parseOutputFile( String input, ref String parseErrorString )
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
        static private Boolean parseContourHeights( String input, ref String parseErrorString )
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
        // TODO : move to library so apps can all share
        // converts hex triplet to 32 bit pixel (note : does NOT expect 0x prefix on string)
        static private Int32 parseColorHexTriplet( String input, ref Boolean parsed, ref String parseErrorString )
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
        // TODO : move to library so apps can share
        static private Boolean parseColor( String input, String colorDescription, ref String parseErrorString, ref Int32 colorOut, ref String colorStringOut )
        {
            Boolean parsed = true;

            String parseError = "";
            colorOut = parseColorHexTriplet(input, ref parsed, ref parseError);

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
        static private Boolean parseBackgroundColor( String input, ref String parseErrorString )
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.BackgroundColor ].Description, ref parseErrorString, ref backgroundColor, ref backgroundColorString );
        }

        // ------------------------------------------------------
        static private Boolean parseContourColor(String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.ContourColor ].Description, ref parseErrorString, ref contourColor, ref contourColorString );
        }

        // ------------------------------------------------------
        static private Boolean parseAlternatingContourColor1( String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.AlternatingColor1 ].Description, ref parseErrorString, ref alternatingContourColor1, ref alternatingContourColor1String );
        }

        // ------------------------------------------------------
        static private Boolean parseAlternatingContourColor2(String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.AlternatingColor2 ].Description, ref parseErrorString, ref alternatingContourColor2, ref alternatingContourColor2String );
        }

        // ------------------------------------------------------
        static private Boolean parseGradientLoColor(String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.GradientLoColor ].Description, ref parseErrorString, ref gradientLoColor, ref gradientLoColorString );
        }

        // ---------------------------------------------------------
        static private Boolean parseGradientHiColor( String input, ref String parseErrorString )
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.GradientHiColor ].Description, ref parseErrorString, ref gradientHiColor, ref gradientHiColorString );
        }

        // ------------------------------------------------------
        static private Boolean parseMode( String input, ref String parseErrorString )
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
        static private Boolean parseReportTimings( String input, ref String parseErrorString )
        {
            reportTimings = true;   // if option is used, it's turned on
            return true;
        }

        // ------------------------------------------------
        static private Boolean parseReportOnly( String input, ref String parseErrorString )
        {
            dataReportOnly = true;    // if option is given, it's on, nothing to parse
            return true;
        }

        // ------------------------------------------------
        static private Boolean parseHelpRequest(String input, ref String parseErrorString)
        {
            helpRequested = true;   // if option is used, it's turned on
            return true;
        }

        static private Boolean parseRectIndex( String str, String coordName, out Int32 coordinateValue, ref String parseErrorString )
        {
            Boolean parsed = false;

            parsed = Int32.TryParse( str, out coordinateValue );

            if ( !parsed )
            {
                parseErrorString = "could not get rect " + coordName + " index from string '" + str + "'";
            }

            return parsed;
        }

        // --------------------------------------------------
        static private Boolean parseRectIndices( String input, ref String parseErrorString )
        {
            Boolean parsed = false;

            string[] coordinates = input.Split( ',' );

            if ( 4 != coordinates.Length )
            {
                // TODO : test
                parseErrorString = "Expected 4 numbers, got " + coordinates.Length.ToString();
                parsed = false;
            }
            else
            {
                // grrrr, this order has to be manually synced with help text string, me kind of annoyed by that...
                // TODO : make constants or enum for coordinate index to side mapping
                parsed = parseRectIndex( coordinates[0], rectTopName, out rectTopIndex, ref parseErrorString );

                if ( parsed )
                {
                    parsed = parseRectIndex(coordinates[1], rectLeftName, out rectLeftIndex, ref parseErrorString);
                }
                if (parsed)
                {
                    parsed = parseRectIndex(coordinates[2], rectBottomName, out rectBottomIndex, ref parseErrorString);
                }
                if (parsed)
                {
                    parsed = parseRectIndex(coordinates[3], rectRightName, out rectRightIndex, ref parseErrorString);
                }

                rectIndicesSpecified = parsed;
            }

            return parsed;
        }

        // --------------------------------------------------
        static private Boolean parseRectCoordinate(String str, String coordName, out float coordinateValue, ref String parseErrorString)
        {
            Boolean parsed = false;

            parsed = float.TryParse(str, out coordinateValue);

            if (!parsed)
            {
                parseErrorString = "could not get rect " + coordName + " coordinate from string '" + str + "'";
            }

            return parsed;
        }

        static private Boolean parseRectCoordinates(String input, ref String parseErrorString)
        {
            Boolean parsed = false;

            string[] coordinates = input.Split( ',' );

            if ( 4 != coordinates.Length )
            {
                parseErrorString = "Expected 4 coordinates, got " + coordinates.Length.ToString();
                parsed = false;
            }
            else
            {
                // grrrr, this order has to be manually synced with help text string, me kind of annoyed by that...
                parsed = parseRectCoordinate( coordinates[0], NorthString, out rectNorthLatitude, ref parseErrorString );

                if ( parsed )
                {
                    parsed = parseRectCoordinate(coordinates[1], WestString, out rectWestLongitude, ref parseErrorString);
                }
                if (parsed)
                {
                    parsed = parseRectCoordinate(coordinates[2], SouthString, out rectSouthLatitude, ref parseErrorString);
                }
                if (parsed)
                {
                    parsed = parseRectCoordinate(coordinates[3], EastString, out rectEastLongitude, ref parseErrorString);
                }

                rectCoordinatesSpecified = parsed;
            }

            return parsed;
        }

        // --------------------------------------------------------------------
        static private void initProgramNotes()
        {
            if ( null == programNotes )
            {
                programNotes = new List<String>();

                programNotes.Add( "Colors are specified as a 'hex triplet' of the form RRGGBB\nwhere RR = red value, GG = green value, and BB = blue value." );
                programNotes.Add( "Color values are given in base-16, and range from 0-255." );
                programNotes.Add( "If rects are specified in both indices and coordinates, the indices will be ignored." );
            }
        }

        // -------------------------------------------------------------------- 
        static private void initOptionSpecifiers()
        {
            outputModeToSpecifierDict = new Dictionary< OutputModeType, OptionSpecifier>( Enum.GetNames(typeof(OutputModeType)).Length );

            // not crazy about these casts, will see how it works in practice
            // TODO : more descriptive help text?
            outputModeToSpecifierDict.Add( OutputModeType.Normal,       new OptionSpecifier {   Specifier = ((char)OutputModeType.Normal).ToString(),      
                                                                                                Description = "Normal",
                                                                                                HelpText = "Normal contour map" });
            outputModeToSpecifierDict.Add( OutputModeType.Gradient,     new OptionSpecifier {   Specifier = ((char)OutputModeType.Gradient).ToString(),    
                                                                                                Description = "Gradient",
                                                                                                HelpText = "Gradient of colors between lowest and highest elevations on map" });
            outputModeToSpecifierDict.Add( OutputModeType.Alternating,  new OptionSpecifier {   Specifier = ((char)OutputModeType.Alternating).ToString(), 
                                                                                                Description = "Alternating",
                                                                                                HelpText = "Contour line colors alternate between altcolor1 and altcolor2" } );
            /*
            outputModeToSpecifierDict.Add( OutputModeType.SeparateMaps, new OptionSpecifier {   Specifier = ((char)OutputModeType.SeparateMaps).ToString(),
                                                                                                Description = "Separate Maps",
                                                                                                HelpText = "Like normal mode, but each contour height is on a separate image." } );
            */

            optionTypeToSpecDict = new Dictionary< OptionType, OptionSpecifier>( Enum.GetNames(typeof(OptionType)).Length );

            optionTypeToSpecDict.Add( OptionType.HelpRequest,   new OptionSpecifier{    Specifier = HelpRequestChar.ToString(), 
                                                                                        Description = "Help (list available options)",
                                                                                        HelpText = ": print all available parameters",
                                                                                        ParseDelegate = parseHelpRequest } );
            optionTypeToSpecDict.Add( OptionType.DataReportOnly,new OptionSpecifier{    Specifier = "datareport",
                                                                                        Description = "Report only",
                                                                                        HelpText = ": Reports details of topo data only, no other output.",
                                                                                        ParseDelegate = parseReportOnly });
            optionTypeToSpecDict.Add( OptionType.ReportTimings, new OptionSpecifier{    Specifier = "timings", 
                                                                                        Description = "Report Timings",
                                                                                        HelpText = ": report Timings",
                                                                                        ParseDelegate = parseReportTimings });
                                                                                        
            optionTypeToSpecDict.Add( OptionType.Mode,          new OptionSpecifier{    Specifier = "mode", 
                                                                                        Description = "Output mode",
                                                                                        HelpText = "<M>: mode (type of output)", 
                                                                                        AllowedValues = outputModeToSpecifierDict.Values.ToList<OptionSpecifier>(), 
                                                                                        ParseDelegate = parseMode });
            optionTypeToSpecDict.Add( OptionType.OutputFile,    new OptionSpecifier{    Specifier = "outfile", 
                                                                                        Description = "Output file",
                                                                                        HelpText = "<OutputFile>: specifies output image file",
                                                                                        ParseDelegate = parseOutputFile });
            optionTypeToSpecDict.Add( OptionType.ContourHeights,new OptionSpecifier{    Specifier = "contours", 
                                                                                        Description = "Contour heights",
                                                                                        HelpText = "<NNN>: Contour height separation (every NNN meters), Minimum : " + MinimumContourHeights,
                                                                                        ParseDelegate = parseContourHeights } );
            optionTypeToSpecDict.Add(OptionType.BackgroundColor,new OptionSpecifier{    Specifier = "bgcolor",
                                                                                        Description = "Background color",
                                                                                        HelpText = "<RRGGBB>: Background Color (defaults to white)",
                                                                                        ParseDelegate = parseBackgroundColor });
            optionTypeToSpecDict.Add(OptionType.ContourColor, new OptionSpecifier{     Specifier = "concolor",
                                                                                        Description = "Contour lines color",
                                                                                        HelpText = "<RRGGBB>: color of contour lines in normal mode",
                                                                                        ParseDelegate = parseContourColor });
            optionTypeToSpecDict.Add(OptionType.AlternatingColor1, new OptionSpecifier{ Specifier = "altcolor1",
                                                                                        Description = "Alternating contour colors 1",
                                                                                        HelpText = "<RRGGBB>: color 1 in alternating mode",
                                                                                        ParseDelegate = parseAlternatingContourColor1 });
            optionTypeToSpecDict.Add(OptionType.AlternatingColor2, new OptionSpecifier{ Specifier = "altcolor2",
                                                                                        Description = "Alternating contour colors 2",
                                                                                        HelpText = "<RRGGBB>: color 2 in alternating mode",
                                                                                        ParseDelegate = parseAlternatingContourColor2 });
            optionTypeToSpecDict.Add(OptionType.GradientLoColor, new OptionSpecifier{   Specifier = "gradlocolor",
                                                                                        Description = "Gradient mode low point color",
                                                                                        HelpText = "<RRGGBB> color of lowest points on map in gradient mode",
                                                                                        ParseDelegate = parseGradientLoColor });
            optionTypeToSpecDict.Add(OptionType.GradientHiColor, new OptionSpecifier{   Specifier = "gradhicolor",
                                                                                        Description = "Gradient mode high point color",
                                                                                        HelpText = "<RRGGBB> color of highest points on map in gradient mode",
                                                                                        ParseDelegate = parseGradientHiColor });
            optionTypeToSpecDict.Add(OptionType.RectIndices, new OptionSpecifier{              Specifier = "recttlbr",
                                                                                        Description = "Rectangle Indices",
                                                                                        HelpText = "<"+rectTopName+","+rectLeftName+","+rectBottomName+","+rectRightName+"> indices of grid within topo data to process and output",
                                                                                        ParseDelegate = parseRectIndices });
            optionTypeToSpecDict.Add(OptionType.RectCoords, new OptionSpecifier{        Specifier = "rectnwse",
                                                                                        Description = "Rectangle Coordinates",
                                                                                        HelpText = "<"+NorthString+","+WestString+","+SouthString+","+EastString+"> specifies grid by (floating point) lat/long values",
                                                                                        ParseDelegate = parseRectCoordinates });
        }

        // --------------------------------------------------------------------------------------
        static private void reportAvailableOptions()
        {
            // aww, no string multiply like Python
            const String indent = "  ";
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

            Console.WriteLine( "Notes:" );
            foreach ( var currentNote in programNotes )
            {
                Console.WriteLine( currentNote );
            }
        }

        // -------------------------------------------------------------------------------------
        static private Boolean parseArgs(string[] args)
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
        static private void reportRectExtents()
        {
            if (rectIndicesSpecified)
            {
                if (rectCoordinatesSpecified)
                {
                    Console.WriteLine("Rect indices ignored.");
                }
                else
                {
                    Console.WriteLine("Rect Indices : ");
                    Console.WriteLine("   Left:" + rectLeftIndex + ", Top:" + rectTopIndex + ", Right:" + rectRightIndex + ", Bottom:" + rectBottomIndex);
                }
            }
            else if (rectCoordinatesSpecified)
            {
                // TODO : this assumes we're working in the western half of the northern hemisphere, need function to convert lat/long to nsew correctly.
                Console.WriteLine("Rect Coordinates : ");
                Console.WriteLine(" " + WestString + ":" + rectWestLongitude + WestChar + ", " + NorthString + ":" + rectNorthLatitude + NorthChar);
                Console.WriteLine(" " + EastString + ":" + rectEastLongitude + WestChar + ", " + SouthString + ":" + rectSouthLatitude + NorthChar);
            }
            else
            {
                Console.WriteLine( "No rect specified, defaulting to entire map." );
            }
        }

        // -------------------------------------------------------------------------------------
        static private void reportSettingsValues()
        {
            Console.WriteLine(ConsoleSectionSeparator);

            // TODO : automate?
            Console.WriteLine("Input file base name : " + inputFileBaseName);

            if ( dataReportOnly )
            {
                Console.WriteLine( "Data report only." );
            }
            else
            {
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

                reportRectExtents();
            }
        }

        // -------------------------------------------------------------------------------------------------------------------
        static private void readDescriptor( FLTTopoData data, String inputFileBaseName )
        {
            try
            {
                data.ReadHeaderFile(inputFileBaseName);
            }
            catch 
            {
                throw;
            }
        }

        // -----------------------------------------------------------------------------------------------------------------------
        static private void readData( FLTTopoData data, String inputFileBaseName )
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            try
            {
                data.ReadDataFile(inputFileBaseName);
            }
            catch
            {
                throw;
            }

            stopwatch.Stop();

            addTiming( "data read", stopwatch.ElapsedMilliseconds );
        }

        // -------------------------------------------------------------------------------------------------------------------
        static Boolean handleRectOptions( FLTTopoData data )
        {
            Boolean validated = true;

            // note : coordinates check first, so indices will be ignored if also specified
            if (rectCoordinatesSpecified)
            {
                Boolean coordinatesValid = true;
                var validationMessages = new List<String>(20);

                try
                {
                    coordinatesValid = data.Descriptor.ValidateCoordinates( rectNorthLatitude, rectWestLongitude, rectSouthLatitude, rectEastLongitude,
                                                                            NorthString, WestString, SouthString, EastString,
                                                                            validationMessages );                                                                            
                }
                catch { throw; }

                // convert to indices
                if ( coordinatesValid )
                {
                    rectLeftIndex = data.Descriptor.LongitudeToColumnIndex( rectWestLongitude );
                    rectRightIndex = data.Descriptor.LongitudeToColumnIndex( rectEastLongitude );
                    rectTopIndex = data.Descriptor.LatitudeToRowIndex( rectNorthLatitude );
                    rectBottomIndex = data.Descriptor.LatitudeToRowIndex( rectSouthLatitude );
                }
                else
                {
                    Console.WriteLine("Errors in specified rect coordinates:");
                    foreach (var msg in validationMessages)
                    {
                        Console.WriteLine("  * " + msg);
                    }

                    validated = false;
                }
            }
            else if (rectIndicesSpecified)
            {
                Boolean indicesValidated = true;
                List<String> validationMessages = null;
                try
                {
                    indicesValidated = data.Descriptor.ValidateRectIndices( rectLeftIndex, rectTopIndex, rectRightIndex, rectBottomIndex,
                                                                            rectLeftName, rectTopName, rectRightName, rectBottomName,
                                                                            out validationMessages);
                }
                catch { throw; }

                if (false == indicesValidated)
                {
                    Console.WriteLine("Errors in rect indices:");
                    foreach (var msg in validationMessages)
                    {
                        Console.WriteLine("  * " + msg);
                    }
                    validated = false;
                }
            }
            else // no rect was specified, default to whole map
            {
                // TODO : may be better to get these extents from descriptor...
                rectLeftIndex = 0;
                rectRightIndex = data.NumCols - 1;
                rectTopIndex = 0;
                rectBottomIndex = data.NumRows - 1;
            }

            return validated;
        }

        // ---------------------------------------------------------------------------------------------------------------------
        private static void dataReport( FLTTopoData data )
        {
            String indent = "  ";

            // note : works with rect spec
            Console.WriteLine("Finding min/max");
            float minElevationInRect = 0;
            int minElevationRow = 0, minElevationColumn = 0;

            float maxElevationInRect = 0;
            int maxElevationRow = 0, maxElevationColumn = 0;

            data.FindMinMaxInRect(  rectLeftIndex, rectTopIndex, rectRightIndex, rectBottomIndex,
                                    ref minElevationInRect, ref minElevationRow, ref minElevationColumn,
                                    ref maxElevationInRect, ref maxElevationRow, ref maxElevationColumn);

            Console.WriteLine(ConsoleSectionSeparator);
            Console.WriteLine(indent + "Descriptor fields:");
            var descriptorValues = data.Descriptor.GetValueStrings();
            foreach (var valStr in descriptorValues)
            {
                Console.WriteLine(indent + indent + valStr);
            }

            Console.WriteLine( ConsoleSectionSeparator );
            Console.WriteLine( "Data report :" );
            Console.WriteLine( indent + "Map extents:" );
            Console.WriteLine( indent + indent + WestString + " : " + data.Descriptor.WestLongitude );
            Console.WriteLine( indent + indent + NorthString + " : " + data.Descriptor.NorthLatitude );
            Console.WriteLine( indent + indent + EastString + " : " + data.Descriptor.EastLongitude );
            Console.WriteLine( indent + indent + SouthString + " : " + data.Descriptor.SouthLatitude );
            Console.WriteLine( indent + "Map Size (degrees):" );
            Console.WriteLine( indent + indent + EastString + "/" + WestString + " : " + data.Descriptor.WidthDegrees );
            Console.WriteLine( indent + indent + NorthString + "/" + SouthString + " : " + data.Descriptor.WidthDegrees);

            // show rect extents
            Console.WriteLine(ConsoleSectionSeparator);
            // min/max report
            reportRectExtents();
            Console.WriteLine();
            Console.WriteLine(indent + "Minimum elevation : " + minElevationInRect);
            // note : this assumes in northern/western hemisphere
            Console.WriteLine( indent + indent + "Found at " + data.Descriptor.RowIndexToLatitude( minElevationRow ) + NorthChar + "," + data.Descriptor.ColumnIndexToLongitude( minElevationColumn ) + WestChar );
            Console.WriteLine( indent + "Maximum elevation : " + maxElevationInRect );
            Console.WriteLine( indent + indent + "Found at " + data.Descriptor.RowIndexToLatitude( maxElevationRow ) + NorthChar + "," + data.Descriptor.ColumnIndexToLongitude( maxElevationColumn ) + WestChar );

            Console.WriteLine();
        }

        // ----------------------------------------------------------------------------------------------------------------------
        static TopoMapGenerator generatorFactory( OutputModeType outputMode, int contourHeights, FLTTopoData data, String outputFilename, int[] rectExtents )
        {
            TopoMapGenerator generator = null;

            switch ( outputMode )
            {
                case OutputModeType.Normal :
                    generator = new NormalTopoMapGenerator( data, contourHeights, outputFilename, rectExtents );
                    generator.setColor( NormalTopoMapGenerator.BackgroundColorKey, backgroundColor );
                    generator.setColor( NormalTopoMapGenerator.ContourLineColorKey, contourColor );
                    break;
                case OutputModeType.Gradient :
                    generator = new GradientTopoMapGenerator(data, contourHeights, outputFilename, rectExtents);
                    generator.setColor(GradientTopoMapGenerator.LowColorKey, gradientLoColor);
                    generator.setColor(GradientTopoMapGenerator.HighColorKey, gradientHiColor);
                    break;
                case OutputModeType.Alternating :
                    generator = new AlternatingColorContourMapGenerator(data, contourHeights, outputFilename, rectExtents);
                    generator.setColor( AlternatingColorContourMapGenerator.BackgroundColorKey, backgroundColor );
                    generator.setColor( AlternatingColorContourMapGenerator.Color1Key, alternatingContourColor1 );
                    generator.setColor( AlternatingColorContourMapGenerator.Color2Key, alternatingContourColor2 );
                    break;
                default :
                    throw new System.InvalidOperationException( "unknown OutputModeType : " + outputMode.ToString() );
            }

            generator.addTimingHandler = addTiming;

            return generator;
        }

        // -------------------------------------------------------------------------------------------------------------------
        // -------------------------------------------------------------------------------------------------------------------
        static void Main(string[] args)
        {
            // ----- startup -----
            initOptionSpecifiers();
            initProgramNotes();

            FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

            System.Console.WriteLine();
            System.Console.WriteLine( BannerMessage );
            System.Console.WriteLine( "Version : " + versionNumber.ToString() );

            // ----- parse program arguments -----
            Boolean parsed = parseArgs(args);

            if ( false == parsed )
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Error : " + parseErrorMessage);

                if ( helpRequested )
                {
                    reportAvailableOptions();
                }
            }
            else // args parsed successfully
            {
                if (helpRequested)
                {
                    reportAvailableOptions();
                }

                // report current options
                reportSettingsValues();

                System.Console.WriteLine( ConsoleSectionSeparator );

                // ---- read descriptor -----
                try
                {
                    readDescriptor( topoData, inputFileBaseName );
                }
                catch 
                { 
                    return; 
                }

                // ---- validate rect options ----
                try
                {
                    Boolean rectValidated = handleRectOptions(topoData);

                    if (false == rectValidated)
                    {
                        return; // TODO : error code?
                    }
                }
                catch { return; }

                // ---- read data ----
                try
                {
                    readData(topoData, inputFileBaseName);
                }
                catch { return; }

                // ---- process ----
                if ( dataReportOnly )
                {
                    dataReport( topoData );
                }
                else
                {
                    // pack extents into array
                    var rectExtents = new int[4];
                    rectExtents[ TopoMapGenerator.RectLeftIndex ] = rectLeftIndex;
                    rectExtents[ TopoMapGenerator.RectTopIndex ] = rectTopIndex;
                    rectExtents[ TopoMapGenerator.RectRightIndex ] = rectRightIndex;
                    rectExtents[ TopoMapGenerator.RectBottomIndex ] = rectBottomIndex;

                    TopoMapGenerator generator = generatorFactory( outputMode, contourHeights, topoData, outputFileName, rectExtents );

                    System.Console.WriteLine(ConsoleSectionSeparator);
                    System.Console.WriteLine("Creating map in " + generator.GetName() + " mode.");

                    generator.Generate();

                }   // end if !dataReportOnly

                if ( reportTimings )
                {
                    PrintTimings();
                }

                System.Console.WriteLine();
            }   // end if args parsed successfully
        }   // end Main()

    }   // end class Program
}   // end namespace


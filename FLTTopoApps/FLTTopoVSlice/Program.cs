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
using OptionUtils;

/* TODO
 * 
 * -make it do what it says
 * -rect input (but how?)
*/

namespace FLTTopoVSlice
{
    class Program
    {
        const float versionNumber = 1.0f;

        // ---- TYPES ----

        // different options the user specifies
        // note that the enums are used as specifiers, hence the lowercases
        enum OptionType
        {
            HelpRequest,        // list options
            outputfile,         // name of output file
            cornerA,            // one end of baseline
            cornerB,            // other end of baseline
            width,              // width (perpendicular to baseline)
            bgcolor,            // color of 'ground' between contour lines
            groundcolor         // color of contour lines 
        };
        // TODO : add timing option

        // ---- CONSTANTS ----
        const String noInputsErrorMessage = "No inputs.";
        const String noInputFileSpecifiedErrorMessage = "No input file specified.";
        const String moreThanOneInputFileSpecifiedErrorString = "More than one input file specified.";
        const String cornerANotSpecifiedErrorMessage = "Corner A not specified.";
        const String cornerBNotSpecifiedErrorMessage = "Corner B not specified.";
        const String widthNotSpecifiedErrorMessage = "Width not specified.";

        const String ConsoleSectionSeparator = "- - - - - - - - - - -";
        const String BannerMessage = "FLT Topo Data Vertical Slicer (run with '?' for options list)";  // "You wouldn't like me when I'm angry."
        const char HelpRequestChar = '?';
        const String LatitudeString = "Latitude";
        const String LongitudeString = "Longitude";
        const String NorthString = "north";
        const String EastString = "east";
        const String SouthString = "south";
        const String WestString = "west";
        const char NorthChar = 'N';
        const char EastChar = 'E';
        const char SouthChar = 'S';
        const char WestChar = 'W';

        const float floatNotSpecifiedValue = float.MaxValue;

        // ---- DEFAULTS ----
        const String DefaultOutputFileSuffix = "_vslice";

        const Int32 DefaultBackgroundColor = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF); // white

        const Int32 DefaultContourColor = (Int32)(((byte)0xFF << 24) | 0); // black

        // list of single line operating notes
        static List<String> programNotes = null;

        // ---- OPTIONS ----
        // supported options
        static Dictionary<OptionType, OptionSpecifier> optionTypeToSpecDict;

        // ---- SETTINGS ----
        static String inputFileBaseName = "";
        static String outputFileBaseName = "";

        static bool helpRequested = false;

        static String parseErrorMessage = "";

        static Int32 backgroundColor = DefaultBackgroundColor;

        static Int32 contourColor = DefaultContourColor;

        // specified by user
        static double cornerALatitude = floatNotSpecifiedValue;
        static double cornerALongitude = floatNotSpecifiedValue;

        static double cornerBLatitude = floatNotSpecifiedValue;
        static double cornerBLongitude = floatNotSpecifiedValue;

        static double rectWidth = floatNotSpecifiedValue;

        // generated based on AB and Width
        static double cornerCLatitude = floatNotSpecifiedValue;
        static double cornerCLongitude = floatNotSpecifiedValue;

        static double cornerDLatitude = floatNotSpecifiedValue;
        static double cornerDLongitude = floatNotSpecifiedValue;

        static int  cornerARow;
        static int  cornerAColumn;

        static int  cornerBRow;
        static int  cornerBColumn;

        static int  cornerCRow;
        static int  cornerCColumn;

        static int  cornerDRow;
        static int  cornerDColumn;

        // ---- timing ----
        // timing logs
        // float = total time, int = total readings
        static Dictionary< String, Tuple<float, int>> timingLog = new Dictionary< String, Tuple<float, int>>(20);

        static void addTiming(String timingEntryName, float timingEntryValueMS)
        { 
            if ( timingLog.ContainsKey( timingEntryName ) )
            {
                // add to existing entry
                var existing = timingLog[ timingEntryName ];

                timingLog[ timingEntryName ] = Tuple.Create<float,int>( existing.Item1 + timingEntryValueMS, existing.Item2 + 1 );
            }
            else // new entry
            {
                timingLog[ timingEntryName ] = Tuple.Create<float,int>( timingEntryValueMS, 1 );
            }
        }

        static void echoTimings()
        {
            String indent = "  ";

            Console.WriteLine(ConsoleSectionSeparator);
            Console.WriteLine("Timings:");
            foreach (var entry in timingLog)
            {
                Console.WriteLine(indent + entry.Key + " took an average of " + entry.Value.Item1 / entry.Value.Item2 + " seconds.");
            }
        }

        // ------------------------------------------------
        static private Boolean parseHelpRequest(String input, ref String parseErrorString)
        {
            helpRequested = true;   // if option is used, it's turned on
            return true;
        }

        // ---- parse delegates ----
        // ------------------------------------------------------
        static private Boolean parseOutputFile(String input, ref String parseErrorString)
        {
            Boolean parsed = false;

            if (input.Length > 0)
            {
                outputFileBaseName = input;
                parsed = true;
            }
            else
            {
                parsed = false;
                parseErrorString = "Output file base name was empty";
            }

            return parsed;
        }

        // ------------------------------------------------------
        // generalized color parsing
        // TODO : move to library so apps can share
        static private Boolean parseColor(String input, String colorDescription, ref String parseErrorString, ref Int32 colorOut )
        {
            Boolean parsed = true;

            String parseError = "";
            colorOut = OptionUtils.ParseSupport.parseColorHexTriplet(input, ref parsed, ref parseError);

            if (false == parsed)
            {
                parseErrorString = "Converting '" + input + "' to " + colorDescription + ", " + parseError;
            }

            return parsed;
        }

        // ------------------------------------------------------
        static private Boolean parseBackgroundColor(String input, ref String parseErrorString)
        {
            return parseColor(input, optionTypeToSpecDict[OptionType.bgcolor].Description, ref parseErrorString, ref backgroundColor );
        }

        // ------------------------------------------------------
        static private Boolean parseGroundColor(String input, ref String parseErrorString)
        {
            return parseColor(input, optionTypeToSpecDict[OptionType.groundcolor].Description, ref parseErrorString, ref contourColor );
        }

        // --------------------------------------------------
        static private Boolean parseCoordinate(String str, String coordName, ref double coordinateValue, ref String parseErrorString)
        {
            Boolean parsed = false;

            parsed = double.TryParse(str, out coordinateValue);

            if (!parsed)
            {
                parseErrorString = "could not get " + coordName + " coordinate from string '" + str + "'";
            }

            return parsed;
        }

        // ------------------------------------------------------------
        static private Boolean parseLatLong( String input, ref double latitude, ref double longitude, ref String parseErrorString )
        {
            Boolean parsed = true;

            string[] coordinates = input.Split( ',' );

            if ( 2 != coordinates.Length )
            {
                parseErrorString = "Expected 2 coordinates, got " + coordinates.Length.ToString();
                parsed = false;
            }
            else
            {
                // grrrr, this order has to be manually synced with help text string, me kind of annoyed by that...
                parsed = parseCoordinate( coordinates[0], LatitudeString, ref latitude, ref parseErrorString );

                if ( parsed )
                {
                    parsed = parseCoordinate( coordinates[1], LongitudeString, ref longitude, ref parseErrorString );
                }
            }

            return parsed;
        }

        // -----------------------------------------------------------------
        static private Boolean parseCornerACoordinate( String input, ref String parseErrorString )
        {
            return parseLatLong( input, ref cornerALatitude, ref cornerALongitude, ref parseErrorString );
        }

        // -----------------------------------------------------------------
        static private Boolean parseCornerBCoordinate(String input, ref String parseErrorString)
        {
            return parseLatLong(input, ref cornerBLatitude, ref cornerBLongitude, ref parseErrorString);
        }

        // ------------------------------------------------------------------
        static private Boolean parseWidth( String input, ref String parseErrorString )
        {
            Boolean parsed = true;

            parsed = double.TryParse( input, out rectWidth );

            if ( false == parsed )
            {
                parseErrorString = "could not convert " + input + " to width value.";
            }

            return parsed;
        }

        // -------------------------------------------------------------------- 
        static private void initOptionSpecifiers()
        {
            optionTypeToSpecDict = new Dictionary<OptionType, OptionSpecifier>(Enum.GetNames(typeof(OptionType)).Length);

            optionTypeToSpecDict.Add(OptionType.HelpRequest, new OptionSpecifier
            {
                Specifier = HelpRequestChar.ToString(),
                Description = "Help (list available options)",
                HelpText = ": print all available parameters",
                ParseDelegate = parseHelpRequest
            });
            optionTypeToSpecDict.Add(OptionType.outputfile, new OptionSpecifier
            {
                Specifier = OptionType.outputfile.ToString(),
                Description = "Output file base name",
                HelpText = "<OutputFile>: specifies output image file base name",
                ParseDelegate = parseOutputFile
            });
            optionTypeToSpecDict.Add(OptionType.bgcolor, new OptionSpecifier
            {
                Specifier = OptionType.bgcolor.ToString(),
                Description = "Background color",
                HelpText = "<RRGGBB>: Background Color (defaults to white)",
                ParseDelegate = parseBackgroundColor
            });
            optionTypeToSpecDict.Add(OptionType.groundcolor, new OptionSpecifier
            {
                Specifier = OptionType.groundcolor.ToString(),
                Description = "Ground lines color",
                HelpText = "<RRGGBB>: color of ground level lines in normal mode",
                ParseDelegate = parseGroundColor
            });
            optionTypeToSpecDict.Add(OptionType.cornerA, new OptionSpecifier
            {
                Specifier = OptionType.cornerA.ToString(),
                Description = "Corner A",
                HelpText = "<lat,long>: Beginning of baseline forming one edge of rect",
                ParseDelegate = parseCornerACoordinate
            });
            optionTypeToSpecDict.Add(OptionType.cornerB, new OptionSpecifier
            {
                Specifier = OptionType.cornerB.ToString(),
                Description = "Corner B",
                HelpText = "<lat,long>: End of baseline forming one edge of rect",
                ParseDelegate = parseCornerBCoordinate
            });
            optionTypeToSpecDict.Add(OptionType.width, new OptionSpecifier
            {
                Specifier = OptionType.width.ToString(),
                Description = "Width",
                HelpText = ": width of rect along edge perpendicular to CornerA->CornerB",
                ParseDelegate = parseWidth
            });

        }

        // --------------------------------------------------------------------
        static private void initProgramNotes()
        {
            if (null == programNotes)
            {
                programNotes = new List<String>();

                programNotes.Add("Colors are specified as a 'hex triplet' of the form RRGGBB\nwhere RR = red value, GG = green value, and BB = blue value.");
                programNotes.Add("Color values are given in base-16, and range from 0-255.");
            }
        }

        // -------------------------------------------------------------------------------------
        static private Boolean HandleNonMatchedParameter(String argString, ref String parseErrorString)
        {
            Boolean handled = false;

            // input file name (we think/hope)
            // see if header and data files exist
            if (System.IO.File.Exists(argString + "." + FLTDataLib.Constants.HEADER_FILE_EXTENSION)
                    && System.IO.File.Exists(argString + "." + FLTDataLib.Constants.DATA_FILE_EXTENSION))
            {
                // specifying more than one input file is not yet  supported
                if (inputFileBaseName.Length > 0)
                {
                    parseErrorString = moreThanOneInputFileSpecifiedErrorString;
                    handled = false;
                }
                else
                {
                    inputFileBaseName = argString;
                    handled = true;
                }
            }

            return handled;
        }

        // -------------------------------------------------------------------------------------
        static private Boolean parseArgs(string[] args)
        {
            Boolean parsed = OptionUtils.ParseSupport.ParseArgs(args, optionTypeToSpecDict.Values.ToList<OptionUtils.OptionSpecifier>(), HandleNonMatchedParameter, ref parseErrorMessage);

            // must specify input file
            if (parsed && (0 == inputFileBaseName.Length))
            {
                parseErrorMessage = noInputFileSpecifiedErrorMessage;
                parsed = false;
            }

            // default output file name to input file name plus something extra
            if (0 == outputFileBaseName.Length)
            {
                outputFileBaseName = inputFileBaseName + DefaultOutputFileSuffix;
            }

            return parsed;
        }

        // --------------------------------------------------------------------------------------
        static private void echoAvailableOptions()
        {
            // aww, no string multiply like Python
            const String indent = "  ";
            const String indent2 = indent + indent;
            const String indent3 = indent2 + indent;
            const String indent4 = indent2 + indent2;

            Console.WriteLine(ConsoleSectionSeparator);
            Console.WriteLine("Options:");
            Console.WriteLine(indent + "Required:");
            Console.WriteLine(indent2 + "InputFile : (no prefix dash) Name of input file (without extension, there must be both an FLT and HDR data file with this name)");

            Console.WriteLine(indent + "Optional:");
            foreach (var currentOptionSpec in optionTypeToSpecDict.Values)
            {
                String currentParamString = indent2 + "-" + currentOptionSpec.Specifier + currentOptionSpec.HelpText;

                Console.WriteLine(currentParamString);

                if (null != currentOptionSpec.AllowedValues)
                {
                    Console.WriteLine(indent3 + "Options : ");
                    foreach (var currentAllowedValueSpec in currentOptionSpec.AllowedValues)
                    {
                        String currentAllowedValueString = indent4 + currentAllowedValueSpec.Specifier + " : " + currentAllowedValueSpec.HelpText;
                        Console.WriteLine(currentAllowedValueString);
                    }
                }
            }
        }

        // --------------------------------------------------------------------------------------
        static private void echoProgramNotes()
        {
            Console.WriteLine();
            Console.WriteLine("Notes:");
            foreach (var currentNote in programNotes)
            {
                Console.WriteLine(currentNote);
            }
        }

        // -------------------------------------------------------------------------------------
        static private void echoSettingsValues()
        {
            Console.WriteLine(ConsoleSectionSeparator);

            // TODO : automate?
            Console.WriteLine("Input file base name : " + inputFileBaseName);

            Console.WriteLine(optionTypeToSpecDict[OptionType.outputfile].Description + " : " + outputFileBaseName );

            Console.WriteLine( "CornerA : " + cornerALatitude + "," + cornerALongitude );
            Console.WriteLine( "CornerB : " +cornerBLatitude + "," + cornerBLongitude );

            // only report colors if changed from default
            Console.WriteLine( "TODO : FINISH SETTINGS FEEDBACK" );
        }

        // -------------------------------------------------------------------------------------------------------------------
        static private void readDescriptor(FLTTopoData data, String inputFileBaseName)
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
        static private void readData(FLTTopoData data, String inputFileBaseName)
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

            addTiming("data read", stopwatch.ElapsedMilliseconds);
        }

        // -------------------------------------------------------------------------------------------------------------------
        // generates four corners of rect specified by user
        // NOTE : DOES NO VALIDATION ON COORDINATES
        static void generateRectCorners(FLTTopoData data, List<String> invalidCoordinates )
        {
            const double MilesPerArcMinute = 1.15077945;  // ye olde nautical mile, (the equatorial distance / 360) / 60 
            const double MilesPerDegree = MilesPerArcMinute * 60.0f;    // about 69 miles (at the equator)
            const double DegreesPerMile = 1.0 / MilesPerDegree;

            Console.WriteLine("TODO : FINISH RECT VALIDATION");

            /*
             We want a rect outlined by corners A,B,C,D. The user provides A and B, and the width (in some units, miles for now) of
             the rect perpendicular to AB. Assuming a clockwise winding of the points and that latitude and longitude 
             form a right handed coordinate system (z representing increasing altitude), generate C and D.
             NOTE : This math works in continuous coordinates of degrees.
             NOTE : Nothing here accounts for changes in latitude with longitude. There will be errors at higher latitudes, but as
             the source data is "square" at any latitude this math will work without making any corrections.
            */
            // create perpendicular to given edge
            double ABLatDelta = cornerBLatitude - cornerALatitude;
            double ABLongDelta = cornerBLongitude - cornerALongitude;

            double ABLength = Math.Abs( Math.Sqrt((ABLatDelta * ABLatDelta) + (ABLongDelta * ABLongDelta)) );

            // need normalized length of AB
            double ABLatDeltaNorm = ABLatDelta / ABLength;
            double ABLongDeltaNorm = ABLongDelta / ABLength;

            // generate normalized perpendicular to AB
            double ABPerpLatDeltaNorm = ABLongDeltaNorm * -1;
            double ABPerpLongDeltaNorm = ABLatDeltaNorm;

            // generate perpendicular to AB, sized to Width
            // TODO : for now assuming width is in miles, may work with other units later
            double widthInDegrees = rectWidth * DegreesPerMile;
            double ABPerpLatDelta = ABPerpLatDeltaNorm * widthInDegrees;
            double ABPerpLongDelta = ABPerpLongDeltaNorm * widthInDegrees;

            Console.WriteLine( "ABPerpLatDelta = " + ABPerpLatDelta );
            Console.WriteLine( "ABPerpLongDelta = " + ABPerpLongDelta );

            // add the perp delta to A to generate D (remember that points wind around)
            cornerDLatitude = cornerALatitude + ABPerpLatDelta;
            cornerDLongitude = cornerALongitude + ABPerpLongDelta;

            // add the perp delta to B to generate C
            cornerCLatitude = cornerBLatitude + ABPerpLatDelta;
            cornerCLongitude = cornerBLongitude + ABPerpLongDelta;
        }

        // -------------------------------------------------------------------------------------
        static void echoMapData( FLTTopoData data )
        {
            String indent = "  ";
            Console.WriteLine( indent + "Map extents:" );
            Console.WriteLine( indent + indent + WestString + " : " + data.Descriptor.WestLongitude );
            Console.WriteLine( indent + indent + NorthString + " : " + data.Descriptor.NorthLatitude );
            Console.WriteLine( indent + indent + EastString + " : " + data.Descriptor.EastLongitude );
            Console.WriteLine( indent + indent + SouthString + " : " + data.Descriptor.SouthLatitude );
            Console.WriteLine( indent + "Map Size (degrees):" );
            Console.WriteLine( indent + indent + EastString + "/" + WestString + " : " + data.Descriptor.WidthDegrees );
            Console.WriteLine( indent + indent + NorthString + "/" + SouthString + " : " + data.Descriptor.WidthDegrees);
        }

        // ------------------------------------------------------------------------------------------------------------------
        static Boolean validateCornerCoordinates( FLTTopoData data )
        {
            var coordinates = new List<Tuple<double,double>>( 4 );

            coordinates.Add( new Tuple<double,double>( cornerALatitude, cornerALongitude ) );
            coordinates.Add( new Tuple<double,double>( cornerBLatitude, cornerBLongitude ) );
            coordinates.Add( new Tuple<double,double>( cornerCLatitude, cornerBLongitude ) );
            coordinates.Add( new Tuple<double,double>( cornerDLatitude, cornerDLongitude ) );

            var invalidCoordinates = data.Descriptor.validateCoordinatesList( coordinates );

            if ( invalidCoordinates.Count > 0 )
            {
                Console.WriteLine( "Specified rect bounds were not all within map.\nInvalid coordinates:");
                foreach ( var coords in invalidCoordinates )
                {
                    Console.WriteLine( "  " + coords.Item1 + "," + coords.Item2 );
                }
            }

            return null == invalidCoordinates ? true : false;
        }

        // ------------------------------------------------------------------------------------------------------------
        // converts rect lat/long coordinates to indices in topo data, and prepares for rasterization of the rect

        static void generateRectIndices( FLTTopoData data )
        {
            // convert to indices
            cornerARow = data.Descriptor.LatitudeToRowIndex( cornerALatitude );
            cornerAColumn = data.Descriptor.LongitudeToColumnIndex( cornerALongitude );

            cornerBRow = data.Descriptor.LatitudeToRowIndex( cornerBLatitude );
            cornerBColumn = data.Descriptor.LongitudeToColumnIndex( cornerBLongitude );

            cornerCRow = data.Descriptor.LatitudeToRowIndex( cornerCLatitude );
            cornerCColumn = data.Descriptor.LongitudeToColumnIndex( cornerCLongitude );

            cornerDRow = data.Descriptor.LatitudeToRowIndex( cornerDLatitude );
            cornerDColumn = data.Descriptor.LongitudeToColumnIndex( cornerDLongitude );

            // detect some particular cases that simplify rasterization

            // is AB oriented north/south?
            if ( cornerAColumn == cornerBColumn )
            {
                // must know if AB is on east or west side
                if ( cornerARow > cornerBRow )
                {
                    // cornerA is south of cornerB (because topo data rows increase from north to south
                    // so, due to clockwise winding of points...
                    /*  B--C
                     *  |  |
                     *  A--D */
                }
                else // cornerARow < cornerBRow
                {
                    /*
                     * D--A
                     * |  |
                     * C--B */
                }
            }
            else if ( cornerARow == cornerBRow ) // is AB oriented east/west?
            {
            }
            else // rect is diagonally oriented
            {
                // orient points around rect so that A is the northernmost corner
            }



        }

        // -------------------------------------------------------------------------------------
        // -------------------------------------------------------------------------------------
        // -------------------------------------------------------------------------------------
        static void Main(string[] args)
        {
            // ----- startup -----
            initOptionSpecifiers();
            initProgramNotes();

            FLTDataLib.FLTTopoData topoData = new FLTDataLib.FLTTopoData();

            System.Console.WriteLine();
            System.Console.WriteLine(BannerMessage);
            System.Console.WriteLine("Version : " + versionNumber.ToString());

            // ----- parse program arguments -----
            Boolean parsed = parseArgs(args);

            if (false == parsed)
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Error : " + parseErrorMessage);

                if (helpRequested)
                {
                    echoAvailableOptions();
                }
            }
            else
            {
                if (helpRequested)
                {
                    echoAvailableOptions();
                    echoProgramNotes();
                }

                // validate that we got corners A/B and a width
                // ugh, TODO : clean this up maybe
                if (        (floatNotSpecifiedValue == cornerALatitude)
                        ||  (floatNotSpecifiedValue == cornerALongitude))
                {
                    Console.WriteLine( "\nError : " + cornerANotSpecifiedErrorMessage );
                    return;
                }
                else if (       (floatNotSpecifiedValue == cornerBLatitude)
                            ||  (floatNotSpecifiedValue == cornerBLongitude))
                {
                    Console.WriteLine("\nError : " + cornerBNotSpecifiedErrorMessage );
                    return;
                }
                else if ( floatNotSpecifiedValue == rectWidth )
                {
                    Console.WriteLine("\nError : " + widthNotSpecifiedErrorMessage );
                    return;
                }

                // report current options
                echoSettingsValues();

                // ---- read descriptor -----
                try
                {
                    readDescriptor(topoData, inputFileBaseName);
                }
                catch
                {
                    return;
                }
                
                // ---- echo map data ----
                Console.WriteLine( ConsoleSectionSeparator );
                echoMapData( topoData );

                // ---- generate/validate rect corners ----
                try
                {
                    var invalidCoordinates = new List<String>(10);
                    Console.WriteLine( "Generating rect corners..." );
                    generateRectCorners(topoData, invalidCoordinates);

                    Console.WriteLine( "cornerA : " + cornerALatitude + "," + cornerALongitude );
                    Console.WriteLine( "cornerB : " + cornerBLatitude + "," + cornerBLongitude );
                    Console.WriteLine( "cornerC : " + cornerCLatitude + "," + cornerCLongitude );
                    Console.WriteLine( "cornerD : " + cornerDLatitude + "," + cornerDLongitude );

                    if ( false == validateCornerCoordinates( topoData ) )
                    {
                        return;
                    }
                }
                catch { return; }

                // ---- 
                generateRectIndices( topoData );


                // ---- read data ----
                try
                {
                    readData(topoData, inputFileBaseName);
                }
                catch { return; }

                Console.WriteLine( "TODO : FINISH OUTPUT");
            }

        }   // end Main()

    }   // end class Program    
}

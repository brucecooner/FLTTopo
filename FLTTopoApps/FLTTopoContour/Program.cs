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
using OptionUtils;

/*
 *  TODO :
 *  -DETECT nonexistent folders in output files!
 *  -file overwrite avoidance or confirmation
    -config file?
    -suppress output option?
    -is my capitalization all over the place?
    - add 'mark coordinates' option ?
    - only use coordinates (internally)
    - nicer logging/messaging interface
	- create 'test' type generator to handle testing (boooring)
	- support svg output in all modes
 * */

namespace FLTTopoContour
{
    class Program
    {
        const int MajorVersion = 1;
        const int MinorVersion = 5;
        const int Revision = 2;
        static String versionNumber = MajorVersion + "." + MinorVersion + "." + Revision;

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
            RectCoords,         // specifies rectangle within topo data to process/output by (floating point) latitude and longitude coordinates 
            ImageHeight,        // desired height of output image
            ImageWidth,         // desired width of output image (only width OR height may be specified)
            AppendCoords,       // append coordinates to vertical slice files (maybe others?)
            ImageHeightScale,   // vertical scale factor for vertical slice image files
            MinRegionPoints,    // regions of this many data points or smaller in quantized data are 'flattened' to next lower height
            SVG,                // produce output in svg format
			MinPointDelta,		// minimum distance between points when creating vector outlines
        };

        // TODO : settle on a capitalization scheme here!!!

        // CONSTANTS
        const String NoInputsErrorMessage = "No inputs.";
        const String NoInputFileSpecifiedErrorMessage = "No input file specified.";
        const String MoreThanOneInputFileSpecifiedErrorString = "More than one input file specified.";
        const String ImageWidthAndHeightSpecifiedErrorString = "Image width and height both specified, only one dimension may be\nspecified, and the other will be calculated.";
        const String ImageDimensionLTEZeroErrorMessage = " was less than or equal to zero.";
		const String OutputMapTypeDoesNotSupportSVGErrorMessage = "Specified output map type does not support SVG output.";
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

        // DEFAULTS
        const String DefaultOutputFileSuffix = "_topo";
        const int DefaultContourHeights = 200;
        const TopoMapGenerator.MapType DefaultMapType = TopoMapGenerator.MapType.Normal;

        const bool DefaultAppendCoordinatesToFilenames = false;

        const float DefaultImageHeightScale = 1.0f;

        //const String DefaultBackgroundColorString = "FFFFFF";
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
        static Dictionary< TopoMapGenerator.MapType, OptionSpecifier > mapTypeToSpecifierDict;

        // ---- SETTINGS ----
        static String inputFileBaseName = "";
        static String _outputFileName = "";

        static Boolean helpRequested = false;

        // type of map to produce
        static TopoMapGenerator.MapType _outputMapType = DefaultMapType;

        static Int32 _contourHeights = DefaultContourHeights;

        static  Boolean reportTimings = false;

        static Boolean dataReportOnly = false;

        static String parseErrorMessage = "";

        static Dictionary<String, Int32> colorsDict = null;

        static Boolean _appendCoordinatesToFilenames = DefaultAppendCoordinatesToFilenames;

        static Boolean _outputSVGFormat = false;

        static float imageHeightScale = DefaultImageHeightScale;

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

        // desired image size
        static Int32 _imageWidth = TopoMapGenerator.ImageDimensionNotSpecifiedValue;
        static Int32 _imageHeight = TopoMapGenerator.ImageDimensionNotSpecifiedValue;

        // minimum region size (zero = use all regions regardless of size)
        static int _minimumRegionDataPoints = 0;

		// minimum allowed distance between vector output points (zero = no minimum)
		static double _minimumVectorOutputPointDelta = 0;

        // list of single line operating notes
        static List<String> programNotes = null;

        // ---- timing ----
        // timing logs
        // float = total time, int = total readings
        static Dictionary< String, Tuple<float, Int32>> timingLog = new Dictionary< String, Tuple<float, Int32>>(20);

        static void addTiming(String timingEntryName, float timingEntryValueMS)
        { 
            if ( timingLog.ContainsKey( timingEntryName ) )
            {
                // add to existing entry
                var existing = timingLog[ timingEntryName ];

                timingLog[ timingEntryName ] = Tuple.Create<float,Int32>( existing.Item1 + timingEntryValueMS, existing.Item2 + 1 );
            }
            else // new entry
            {
                timingLog[ timingEntryName ] = Tuple.Create<float,Int32>( timingEntryValueMS, 1 );
            }
        }

        static void echoTimings()
        {
            String indent = "  ";

            Console.WriteLine(ConsoleSectionSeparator);
            Console.WriteLine("Timings (average) :");
            foreach (var entry in timingLog)
            {
                Console.WriteLine(indent + entry.Key + " : " + (entry.Value.Item1 / entry.Value.Item2) / 1000.0 + " seconds.");
            }
        }

        // ---- parse delegates ----
        // ------------------------------------------------------
        static private Boolean parseOutputFile( String input, ref String parseErrorString )
        {
            Boolean parsed = false;

            if ( input.Length > 0 )
            {
                _outputFileName = input;
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

            parsed = Int32.TryParse( input, out _contourHeights );

            if ( false == parsed )
            {
                parseErrorString = "Specified contour height '" + input + "' could not be converted to a number.";
            }
            else
            {
                if ( _contourHeights < MinimumContourHeights )
                {
                    parsed = false;
                    parseErrorMessage = "Minimum allowed contour height is " + MinimumContourHeights;
                }
            }

            return parsed;
        }

        // ------------------------------------------------------
        // generalized color parsing
        static private Boolean parseColor( String input, String colorDescription, ref String parseErrorString, String colorsDictKey )
        {
            Boolean parsed = true;

            String parseError = "";
            Int32 color = OptionUtils.ParseSupport.parseColorHexTriplet(input, ref parsed, ref parseError);

            if (false == parsed)
            {
                parseErrorString = "Converting '" + input + "' to " + colorDescription + ", " + parseError;
            }
            else
            {
                colorsDict[ colorsDictKey ] = color;
            } 

            return parsed;
        }

        // ------------------------------------------------------
        static private Boolean parseBackgroundColor( String input, ref String parseErrorString )
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.BackgroundColor ].Description, ref parseErrorString, TopoMapGenerator.colorType.bgcolor.ToString() );
        }

        // ------------------------------------------------------
        static private Boolean parseContourColor(String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.ContourColor ].Description, ref parseErrorString, TopoMapGenerator.colorType.concolor.ToString() );
        }

        // ------------------------------------------------------
        static private Boolean parseAlternatingContourColor1( String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.AlternatingColor1 ].Description, ref parseErrorString, TopoMapGenerator.colorType.altcolor1.ToString() );
        }

        // ------------------------------------------------------
        static private Boolean parseAlternatingContourColor2(String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.AlternatingColor2 ].Description, ref parseErrorString, TopoMapGenerator.colorType.altcolor2.ToString() );
        }

        // ------------------------------------------------------
        static private Boolean parseGradientLoColor(String input, ref String parseErrorString)
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.GradientLoColor ].Description, ref parseErrorString, TopoMapGenerator.colorType.gradlocolor.ToString() );
        }

        // ---------------------------------------------------------
        static private Boolean parseGradientHiColor( String input, ref String parseErrorString )
        {
            return parseColor( input, optionTypeToSpecDict[ OptionType.GradientHiColor ].Description, ref parseErrorString, TopoMapGenerator.colorType.gradhicolor.ToString() );
        }

        // ------------------------------------------------------
        static private Boolean parseMapType( String input, ref String parseErrorString )
        {
            Boolean parsed = false;

            foreach (KeyValuePair<TopoMapGenerator.MapType, OptionSpecifier> currentModeSpec in mapTypeToSpecifierDict)
            {
                if ( input.Equals( currentModeSpec.Value.Specifier ) )
                {
                    _outputMapType = currentModeSpec.Key;
                    parsed = true;
                }
            }

            if ( false == parsed )
            {
                parseErrorString = "Specified map type '" + input + "' not recognized.";
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

        // ----------------------------------------------------------------------
        static private bool parseImageWidth( String input, ref String parseErrorString )
        {
            bool parsed =true;

            int width;

            parsed = Int32.TryParse( input,  out width );

            if ( parsed )
            {
                _imageWidth = width;
            }
            else
            {
                parseErrorString = "Unable to get image width from '" + input + "'";
            }

            return parsed;
        }

        // ----------------------------------------------------------------------
        static private bool parseImageHeight( String input, ref String parseErrorString )
        {
            bool parsed =true;

            int height;

            parsed = Int32.TryParse( input,  out height );

            if ( parsed )
            {
                _imageHeight = height;
            }
            else
            {
                parseErrorString = "Unable to get image height from '" + input + "'";
            }

            return parsed;
        }

        // ------------------------------------------------------------------------
        static private bool parseImageHeightScale( String input, ref String parseErrorString )
        {
            bool parsed =true;

            float scale;

            parsed = float.TryParse( input,  out scale );

            if ( parsed )
            {
                if ( scale < 0 )
                {
                    parseErrorString = "Image height scale must be greater than zero.";
                    parsed = false;
                }
                else
                {
                    imageHeightScale = scale;
                }
            }
            else
            {
                parseErrorString = "Unable to get image height from '" + input + "'";
            }

            return parsed;
        }

        // --------------------------------------------------------------------
        static private bool parseAppendCoordinates( String input, ref String parseErrorString )
        {
            _appendCoordinatesToFilenames = true;   // if option is used, it's turned on
            return true;
        }

        // ---------------------------------------------------------------------
        static private bool parseMinumRegionPoints( String input, ref String parseErrorString )
        {
            bool parsed = true;

            int minPoints;

            parsed = int.TryParse(input, out minPoints);

            if (parsed)
            {
                if (minPoints <= 0)
                {
                    parseErrorString = "Minimum region points must be greater than zero.";
                    parsed = false;
                }
                else
                {
                    _minimumRegionDataPoints = minPoints;
                }
            }
            else
            {
                parseErrorString = "Unable to get minimum region points from '" + input + "'";
            }

            return parsed;
        }

        // -----------------------------------------------------------------------
        static private bool parseSVGFormat( String input, ref String parseErrorString )
        {
            _outputSVGFormat = true;
            return true;
        }

		// -----------------------------------------------------------------------
		static private bool parseMinimumPointDelta( String input, ref String parseErrorString )
		{
            bool parsed = true;

            int minDistance;

            parsed = int.TryParse(input, out minDistance);

            if (parsed)
            {
                if (minDistance <= 0)
                {
                    parseErrorString = "Minimum distance must be greater than zero.";
                    parsed = false;
                }
                else
                {
                    _minimumVectorOutputPointDelta = minDistance;
                }
            }
            else
            {
                parseErrorString = "Unable to get minimum distance from '" + input + "'";
            }

            return parsed;
		}

		// -------------------------------------------------------------------- 
        static private void initOptionSpecifiers()
        {
            // -- map type options --
            mapTypeToSpecifierDict = new Dictionary<TopoMapGenerator.MapType, OptionSpecifier>(Enum.GetNames(typeof(TopoMapGenerator.MapType)).Length);

            // TODO : more descriptive help text?
            mapTypeToSpecifierDict.Add(TopoMapGenerator.MapType.Normal,
                new OptionSpecifier { Specifier = TopoMapGenerator.MapType.Normal.ToString().ToLower(),
                    Description = "Normal",
                    HelpText = "Normal contour map" });
            mapTypeToSpecifierDict.Add(TopoMapGenerator.MapType.Gradient,
                new OptionSpecifier { Specifier = TopoMapGenerator.MapType.Gradient.ToString().ToLower(),
                    Description = "Gradient",
                    HelpText = "Solid colors, from lo color to hi color with height" });
            mapTypeToSpecifierDict.Add(TopoMapGenerator.MapType.AlternatingColors,
                new OptionSpecifier { Specifier = TopoMapGenerator.MapType.AlternatingColors.ToString().ToLower(),
                    Description = "Alternating",
                    HelpText = "Line colors alternate between altcolor1 and altcolor2" });
            mapTypeToSpecifierDict.Add(TopoMapGenerator.MapType.HorizontalSlice,
                new OptionSpecifier { Specifier = TopoMapGenerator.MapType.HorizontalSlice.ToString().ToLower(),
                    Description = "Horizontal slice",
                    HelpText = "normal map, but each contour in a separate image." });
            mapTypeToSpecifierDict.Add(TopoMapGenerator.MapType.VerticalSliceNS,
                new OptionSpecifier { Specifier = TopoMapGenerator.MapType.VerticalSliceNS.ToString().ToLower(),
                    Description = "Vertical slice" + NorthString + "/" + SouthString,
                    HelpText = "vertical slices, oriented " + NorthString + "/" + SouthString + " at <contourHeights> intervals" });
            mapTypeToSpecifierDict.Add(TopoMapGenerator.MapType.VerticalSliceEW,
                new OptionSpecifier { Specifier = TopoMapGenerator.MapType.VerticalSliceEW.ToString().ToLower(),
                    Description = "Vertical slice" + EastString + "/" + WestString,
                    HelpText = "vertical slices, oriented " + EastString + "/" + WestString + " at <contourHeights> intervals" });

            optionTypeToSpecDict = new Dictionary< OptionType, OptionSpecifier>( Enum.GetNames(typeof(OptionType)).Length );

            optionTypeToSpecDict.Add( OptionType.HelpRequest,   new OptionSpecifier{    
                Specifier = HelpRequestChar.ToString(), 
                Description = "Help (list available options + program notes)",
                HelpText = "print all options + program notes",
                ParseDelegate = parseHelpRequest } );
            optionTypeToSpecDict.Add( OptionType.DataReportOnly,new OptionSpecifier{    
                Specifier = "datareport",
                Description = "Report only",
                HelpText = "details of topo data only, no map produced",
                ParseDelegate = parseReportOnly });
            optionTypeToSpecDict.Add( OptionType.ReportTimings, new OptionSpecifier{    
                Specifier = "timings", 
                Description = "Report Timings",
                HelpText = "report timings upon completion",
                ParseDelegate = parseReportTimings });
            optionTypeToSpecDict.Add(OptionType.SVG, new OptionSpecifier{
                Specifier = "svg",
                Description = "Scalar Vector Graphics",
                HelpText = "output is in Scalar Vector Graphics format (not supported for all map types)",
                ParseDelegate = parseSVGFormat,
                ExpectsValue = false });
            optionTypeToSpecDict.Add( OptionType.Mode,          new OptionSpecifier{    
                Specifier = "maptype", 
                Description = "Map Type",
                HelpText = "<M>: map type",
                AllowedValues = mapTypeToSpecifierDict.Values.ToList<OptionSpecifier>(), 
                ParseDelegate = parseMapType,
                ExpectsValue = true });
            optionTypeToSpecDict.Add( OptionType.OutputFile,    new OptionSpecifier{    
                Specifier = "outfile", 
                Description = "Output file",
                HelpText = "<OutputFile>: specifies output image file",
                ParseDelegate = parseOutputFile,
                ExpectsValue = true });
            optionTypeToSpecDict.Add( OptionType.ContourHeights,new OptionSpecifier{    
                Specifier = "contours", 
                Description = "Contour heights",
                HelpText = "<NNN>: Contour height separation (every NNN meters), Minimum : " + MinimumContourHeights,
                ParseDelegate = parseContourHeights,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.BackgroundColor,new OptionSpecifier{    
                Specifier = TopoMapGenerator.colorType.bgcolor.ToString(),
                Description = "Background color",
                HelpText = "<RRGGBB>: Background Color (defaults to white)",
                ParseDelegate = parseBackgroundColor,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.ContourColor, new OptionSpecifier{     
                Specifier = TopoMapGenerator.colorType.concolor.ToString(),
                Description = "Contour lines color",
                HelpText = "<RRGGBB>: color of contour lines in normal mode",
                ParseDelegate = parseContourColor,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.AlternatingColor1, new OptionSpecifier{ 
                Specifier = TopoMapGenerator.colorType.altcolor1.ToString(),
                Description = "Alternating contour colors 1",
                HelpText = "<RRGGBB>: color 1 in alternating mode",
                ParseDelegate = parseAlternatingContourColor1,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.AlternatingColor2, new OptionSpecifier{ 
                Specifier = TopoMapGenerator.colorType.altcolor2.ToString(),
                Description = "Alternating contour colors 2",
                HelpText = "<RRGGBB>: color 2 in alternating mode",
                ParseDelegate = parseAlternatingContourColor2,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.GradientLoColor, new OptionSpecifier{   
                Specifier = TopoMapGenerator.colorType.gradlocolor.ToString(),
                Description = "Gradient mode low point color",
                HelpText = "<RRGGBB> color of lowest points on map in gradient mode",
                ParseDelegate = parseGradientLoColor,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.GradientHiColor, new OptionSpecifier{   
                Specifier = TopoMapGenerator.colorType.gradhicolor.ToString(),
                Description = "Gradient mode high point color",
                HelpText = "<RRGGBB> color of highest points on map in gradient mode",
                ParseDelegate = parseGradientHiColor,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.RectIndices, new OptionSpecifier{       
                Specifier = "recttlbr",
                Description = "Rectangle Indices",
                HelpText = "<"+rectTopName+","+rectLeftName+","+rectBottomName+","+rectRightName+"> indices of grid within topo data to process and output",
                ParseDelegate = parseRectIndices,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.RectCoords, new OptionSpecifier{        
                Specifier = "rectnwse",
                Description = "Rectangle Coordinates",
                HelpText = "<"+NorthString+","+WestString+","+SouthString+","+EastString+"> specifies grid by (floating point) lat/long values",
                ParseDelegate = parseRectCoordinates,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.ImageWidth, new OptionSpecifier{        
                Specifier="imgwidth",
                Description = "Image Width",
                HelpText = "<width> output image width",
                ParseDelegate = parseImageWidth,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.ImageHeight, new OptionSpecifier{       
                Specifier="imgheight",
                Description = "Image Height",
                HelpText = "<height> output image height",
                ParseDelegate = parseImageHeight,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.AppendCoords,   new OptionSpecifier{    
                Specifier="appendcoords",
                Description="Append Coordinates",
                HelpText = "append coordinates to vertical slice output filenames",
                ParseDelegate = parseAppendCoordinates,
                ExpectsValue = false });
            optionTypeToSpecDict.Add(OptionType.ImageHeightScale, new OptionSpecifier{
                Specifier = "imgheightscale",
                Description = "Image Height Scale",
                HelpText = "<scale> scales height of vertical slice images",
                ParseDelegate = parseImageHeightScale,
                ExpectsValue = true });
            optionTypeToSpecDict.Add(OptionType.MinRegionPoints, new OptionSpecifier{
                Specifier = "minregionpoints",
                Description = "Minimum Data Points",
                HelpText = "<integer> exclude regions of this many data points or fewer (in quantized source data) from output",
                ParseDelegate = parseMinumRegionPoints,
                ExpectsValue = true });
			optionTypeToSpecDict.Add(OptionType.MinPointDelta, new OptionSpecifier{
				Specifier = "minpointdelta",
				Description = "Minimum Point Delta",
				HelpText = "<meters> minimum distance between vector output points",
				ParseDelegate = parseMinimumPointDelta,
				ExpectsValue = true });
        }

        // --------------------------------------------------------------------
        static private void initProgramNotes()
        {
            if ( null == programNotes )
            {
                programNotes = new List<String>();

                // yeah yeah, could be an external resource, but I prefer the portability of them being in the code
                programNotes.Add("-Example usage (assumes existence of MyInputFile.hdr and MyInputFile.flt) : ");
                programNotes.Add("  flttopocontour MyInputFile outputfile=MyOutputFile maptype=gradient gradlocolor=FF0000 gradhicolor=0000ff");
                programNotes.Add("");
                programNotes.Add("-Colors are specified as a 'hex triplet' of the form RRGGBB\nwhere RR = red value, GG = green value, and BB = blue value.");
                programNotes.Add("-Color values are given in base-16, and range from 0-FF." );
                programNotes.Add("-Rect 'top' and 'bottom' indices are actually reversed (top < bottom), since topo data is stored from north to south." );
                programNotes.Add("-If a rect is specified in both indices and coordinates, the indices will be ignored." );
                programNotes.Add("-The equal sign between options and values may be omitted (e.g. : gradlocolorFF0000)." );
                programNotes.Add("-If only imgWidth or imgHeight is specified, the other is calculated\nwith respect to the aspect ratio of the input rect." );
                programNotes.Add("-If the output image is not scaled, there is a 1:1 mapping between data points in the source height data and pixels in the output image");
                programNotes.Add("-minRegionPoints excludes regions of a certain number of points in the source height data (counted post quantization)");
                programNotes.Add("   -thus a minRegionPoints setting of 100 would prevent the showing of any contours that did not enclose at least 100");
                programNotes.Add("    points in the source data (region can be arbitrarily shaped)");
                programNotes.Add("   -this removes very small 'nuisance' regions from data but take care, very large settings may remove important contours");
                programNotes.Add("   -this feature is new and has received minimal testing, results may not be exact");
            }
        }


        // --------------------------------------------------------------------------------------
        static private void echoAvailableOptions()
        {
            // aww, no string multiply like Python
            const String indent = "  ";
            const String indent2 = indent + indent;
            const String indent3 = indent2 + indent;
            const String indent4 = indent2 + indent2;

            Console.WriteLine( ConsoleSectionSeparator );
            Console.WriteLine( "Options:" );
            Console.WriteLine( indent + "Required:" );
            Console.WriteLine( indent2 + "Name of input file (without extension, there must be both an FLT and HDR data file with this name)" );

            Console.WriteLine( indent + "Optional:");
            Console.WriteLine( indent + "('=' indicates option is followed by equal sign, then value for <>)");
            foreach ( var currentOptionSpec in optionTypeToSpecDict.Values )
            {
                String separator = currentOptionSpec.ExpectsValue ? "=" : " : ";

                String currentParamString = indent2 + currentOptionSpec.Specifier + separator + currentOptionSpec.HelpText;

                Console.WriteLine(currentParamString);

                if ( null != currentOptionSpec.AllowedValues )
                {
                    Console.WriteLine( indent3 + "Options for " + currentOptionSpec.Description + " : " );
                    foreach ( var currentAllowedValueSpec in currentOptionSpec.AllowedValues )
                    {
                        String currentAllowedValueString  = indent4 + currentAllowedValueSpec.Specifier + " : " + currentAllowedValueSpec.HelpText;
                        Console.WriteLine( currentAllowedValueString );
                    }
                }
            }
        }

        // --------------------------------------------------------------------------------------
        static private void echoProgramNotes()
        {
            Console.WriteLine();
            Console.WriteLine( "Notes:" );
            foreach ( var currentNote in programNotes )
            {
                Console.WriteLine( currentNote );
            }
        }

        // -------------------------------------------------------------------------------------
        static private Boolean HandleNonMatchedParameter( String argString, ref String parseErrorString )
        {
            Boolean handled = false;

            // input file name (we think/hope)
            // see if header and data files exist
            if (        System.IO.File.Exists( argString + "." + FLTDataLib.Constants.HEADER_FILE_EXTENSION )
                    &&  System.IO.File.Exists( argString + "." + FLTDataLib.Constants.DATA_FILE_EXTENSION) )
            {
                // specifying more than one input file is not yet  supported
                if (inputFileBaseName.Length > 0)
                {
                    parseErrorString = MoreThanOneInputFileSpecifiedErrorString;
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
            Boolean parsed = OptionUtils.ParseSupport.ParseArgs( args, optionTypeToSpecDict.Values.ToList<OptionUtils.OptionSpecifier>(), HandleNonMatchedParameter, ref parseErrorMessage );

            // must specify input file
            if ( parsed && (0 == inputFileBaseName.Length) )
            {
                parseErrorMessage = NoInputFileSpecifiedErrorMessage;
                parsed = false;
            }

            // default output file name to input file name plus something extra
            if ( 0 == _outputFileName.Length )
            {
                _outputFileName = inputFileBaseName + DefaultOutputFileSuffix;
            }

            return parsed;
        }

        // -------------------------------------------------------------------------------------
        static private void echoRectExtents()
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
        static private void echoImageSize()
        {
            Console.WriteLine( "Image width: " + (TopoMapGenerator.ImageDimensionSpecified( _imageWidth ) ? _imageWidth.ToString() : "not specified" ) );
            Console.WriteLine( "Image height: " + (TopoMapGenerator.ImageDimensionSpecified( _imageHeight ) ? _imageHeight.ToString() : "not specified" ) );
            Console.WriteLine( "Image height scale: " + imageHeightScale );
        }

        // -------------------------------------------------------------------------------------
        static private void echoSettingsValues()
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
                Console.WriteLine(optionTypeToSpecDict[OptionType.OutputFile].Description + " : " + _outputFileName);
                Console.WriteLine(optionTypeToSpecDict[OptionType.Mode].Description + " : " + mapTypeToSpecifierDict[_outputMapType].Description);
                Console.WriteLine(optionTypeToSpecDict[OptionType.ContourHeights].Description + " : " + _contourHeights);
                Console.WriteLine(optionTypeToSpecDict[OptionType.ReportTimings].Description + " : " + (reportTimings ? "yes" : "no"));
                Console.WriteLine(optionTypeToSpecDict[OptionType.AppendCoords].Description + " : " + (_appendCoordinatesToFilenames ? "yes" : "no"));
				Console.WriteLine(optionTypeToSpecDict[OptionType.SVG].Description + " : " + (_outputSVGFormat ? "yes" : "no"));
				// vector related options
				if (_outputSVGFormat)
				{
					Console.WriteLine(optionTypeToSpecDict[OptionType.MinPointDelta].Description + " : " + _minimumVectorOutputPointDelta);
				}

                if (_minimumRegionDataPoints > 0)
                {
                    Console.WriteLine(optionTypeToSpecDict[OptionType.MinRegionPoints].Description + " : " + _minimumRegionDataPoints);
                }

                // only report colors if changed from default
                if ( colorsDict[ TopoMapGenerator.colorType.bgcolor.ToString() ] != DefaultBackgroundColor )
                {
                    Console.WriteLine(optionTypeToSpecDict[OptionType.BackgroundColor].Description + " : " + OptionUtils.ParseSupport.ARGBColorToHexTriplet(colorsDict[TopoMapGenerator.colorType.bgcolor.ToString()]));
                }
                if (colorsDict[TopoMapGenerator.colorType.concolor.ToString()] != DefaultContourColor)
                {
                    Console.WriteLine(optionTypeToSpecDict[OptionType.ContourColor].Description + " : " + OptionUtils.ParseSupport.ARGBColorToHexTriplet( colorsDict[ TopoMapGenerator.colorType.concolor.ToString() ] ) );
                }
                if ( colorsDict[TopoMapGenerator.colorType.altcolor1.ToString()] != DefaultAlternatingColor1 )
                {
                    Console.WriteLine(optionTypeToSpecDict[OptionType.AlternatingColor1].Description + " : " + OptionUtils.ParseSupport.ARGBColorToHexTriplet(colorsDict[TopoMapGenerator.colorType.altcolor1.ToString()]));
                }
                if ( colorsDict[TopoMapGenerator.colorType.altcolor2.ToString() ] != DefaultAlternatingColor2)
                {
                    Console.WriteLine(optionTypeToSpecDict[OptionType.AlternatingColor2].Description + " : " + OptionUtils.ParseSupport.ARGBColorToHexTriplet(colorsDict[TopoMapGenerator.colorType.altcolor2.ToString()]));
                }
                if ( colorsDict[TopoMapGenerator.colorType.gradlocolor.ToString() ] != DefaultGradientLoColor)
                {
                    Console.WriteLine(optionTypeToSpecDict[OptionType.GradientLoColor].Description + " : " + OptionUtils.ParseSupport.ARGBColorToHexTriplet(colorsDict[TopoMapGenerator.colorType.gradlocolor.ToString()]));
                }
                if ( colorsDict[TopoMapGenerator.colorType.gradhicolor.ToString() ] != DefaultGradientHiColor)
                {
                    Console.WriteLine(optionTypeToSpecDict[OptionType.GradientHiColor].Description + " : " + OptionUtils.ParseSupport.ARGBColorToHexTriplet(colorsDict[TopoMapGenerator.colorType.gradhicolor.ToString()]));
                }

                echoRectExtents();
                echoImageSize();
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
        // must be called after rect options are validated and input rect indices are calculated
        private static bool validateImageSizeOptions( FLTTopoData data )
        {
            bool validated = true;

            if ( TopoMapGenerator.ImageDimensionSpecified( _imageWidth ) )
            {
                // validate
                if ( _imageWidth <= 0 )
                {
                    Console.WriteLine( "specified " + optionTypeToSpecDict[ OptionType.ImageWidth].Description + ImageDimensionLTEZeroErrorMessage );
                    validated = false;
                }
            }

            if ( TopoMapGenerator.ImageDimensionSpecified( _imageHeight ) )
            {
                if ( _imageHeight <= 0 )
                {
                    Console.WriteLine( "specified " + optionTypeToSpecDict[ OptionType.ImageHeight].Description + ImageDimensionLTEZeroErrorMessage );
                    validated = false;
                }
            }

            /*
            int rectWidth = rectRightIndex - rectLeftIndex + 1;    // TODO : maybe DRY this
            int rectHeight = rectBottomIndex - rectTopIndex + 1;

            if (        ( ImageDimensionNotSpecifiedValue == imageWidth )
                    &&  ( ImageDimensionNotSpecifiedValue == imageHeight ) )
            {
                // neither specified, use rect size
                imageWidth = rectWidth;
                imageHeight = rectHeight;
            }
            else if (       ( ImageDimensionNotSpecifiedValue == imageWidth )
                        &&  ( ImageDimensionNotSpecifiedValue != imageHeight ) )
            {
                // height specified, validate it is > 0
                if ( imageHeight <= 0 )
                {
                    Console.WriteLine( ImageDimensionLTEZeroErrorMessage );
                    validated = false;
                }
                else
                {
                    imageWidth = (int)(imageHeight * (rectWidth / (float)rectHeight));
                }
            }
            else if (       ( ImageDimensionNotSpecifiedValue == imageHeight )
                        &&  ( ImageDimensionNotSpecifiedValue != imageWidth ) )
            {
                // width specified, validate it is > 0
                if ( imageWidth <= 0 )
                {
                    Console.WriteLine( ImageDimensionLTEZeroErrorMessage );
                    validated = false;
                }
                else
                {
                    // width was specified, calculate height
                    imageHeight = (int)(imageWidth * (rectHeight / (float)rectWidth));
                }
            }
            // else user specified both, no changes
            */
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
            Console.WriteLine( "Map data report :" );
            Console.WriteLine(indent + "Cell size (meters): " + data.MetersPerCell());
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
            echoRectExtents();
            Console.WriteLine();
            Console.WriteLine(indent + "Minimum elevation : " + minElevationInRect);
            // note : this assumes in northern/western hemisphere
            Console.WriteLine( indent + indent + "Found at " + data.Descriptor.RowIndexToLatitude( minElevationRow ) + NorthChar + "," + data.Descriptor.ColumnIndexToLongitude( minElevationColumn ) + WestChar );
            Console.WriteLine( indent + "Maximum elevation : " + maxElevationInRect );
            Console.WriteLine( indent + indent + "Found at " + data.Descriptor.RowIndexToLatitude( maxElevationRow ) + NorthChar + "," + data.Descriptor.ColumnIndexToLongitude( maxElevationColumn ) + WestChar );

            Console.WriteLine();
        }

        // -------------------------------------------------------------
        static void initColorsDictionary()
        {
            colorsDict = new Dictionary<string,int>(15);

            colorsDict[ TopoMapGenerator.colorType.concolor.ToString() ] = DefaultContourColor;
            colorsDict[ TopoMapGenerator.colorType.bgcolor.ToString() ] = DefaultBackgroundColor;

            colorsDict[ TopoMapGenerator.colorType.altcolor1.ToString() ] = DefaultAlternatingColor1;
            colorsDict[ TopoMapGenerator.colorType.altcolor2.ToString() ] = DefaultAlternatingColor2;

            colorsDict[ TopoMapGenerator.colorType.gradlocolor.ToString() ] = DefaultGradientLoColor;
            colorsDict[ TopoMapGenerator.colorType.gradhicolor.ToString() ] = DefaultGradientHiColor;
        }

        // -------------------------------------------------------------------------------------------------------------------
        // -------------------------------------------------------------------------------------------------------------------
        static int Main(string[] args)
        {
            const int ReturnErrorCode = 1;
            const int ReturnSuccess = 0;

            // ----- startup -----
            initOptionSpecifiers();
            initProgramNotes();
            initColorsDictionary();

            FLTDataLib.FLTTopoData _topoData = new FLTDataLib.FLTTopoData();

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

                if (helpRequested)
                {
                    echoAvailableOptions();
                    echoProgramNotes();
                }
                else
                {
                    Console.WriteLine("\n(run with '" + HelpRequestChar + "' to see help)");
                }

                return ReturnErrorCode;
            }
            else // args parsed successfully
            {
                if (helpRequested)
                {
                    echoAvailableOptions();
                    echoProgramNotes();
                }

                System.Console.WriteLine( ConsoleSectionSeparator );

                // ---- read descriptor -----
                try
                {
                    readDescriptor( _topoData, inputFileBaseName );
                }
                catch 
                { 
                    return ReturnErrorCode; 
                }

                // ---- validate rect options ----
                try
                {
                    Boolean rectValidated = handleRectOptions(_topoData);

                    if (false == rectValidated)
                    {
                        return ReturnErrorCode;
                    }
                }
                catch { return ReturnErrorCode; }

                // ---- validate image size options ----
                try
                {
                    Boolean imageSizeValidated = validateImageSizeOptions(_topoData);

                    if (false == imageSizeValidated)
                    {
                        return ReturnErrorCode;
                    }
                }
                catch { return ReturnErrorCode; }
				// ---- validate svg is supported (if specified) ---- 
				try
				{
					if ( _outputSVGFormat )
					{
						if (false == TopoMapGenerator.mapTypeSupportsSVG( _outputMapType ))
						{
							Console.WriteLine( OutputMapTypeDoesNotSupportSVGErrorMessage );
							
							return ReturnErrorCode;
						}
					}
				}
				catch { return ReturnErrorCode; }

                // report current options
                echoSettingsValues();

                // ---- read data ----
                try
                {
                    readData(_topoData, inputFileBaseName);
                }
                catch { return ReturnErrorCode; }

                // ---- process ----
                if ( dataReportOnly )
                {
                    dataReport( _topoData );
                }
                else
                {
                    // pack extents into array
                    var _rectExtents = new int[4];
                    _rectExtents[ TopoMapGenerator.RectLeftIndex ] = rectLeftIndex;
                    _rectExtents[ TopoMapGenerator.RectTopIndex ] = rectTopIndex;
                    _rectExtents[ TopoMapGenerator.RectRightIndex ] = rectRightIndex;
                    _rectExtents[ TopoMapGenerator.RectBottomIndex ] = rectBottomIndex;

                    var setupData = new TopoMapGenerator.GeneratorSetupData();
                    setupData.Type = _outputMapType;
                    setupData.ContourHeights = _contourHeights;
                    setupData.Data = _topoData;
                    setupData.OutputFilename = _outputFileName;
                    setupData.RectIndices = _rectExtents;
                    setupData.ImageWidth = _imageWidth;
                    setupData.ImageHeight = _imageHeight;
                    setupData.AppendCoordinatesToFilenames = _appendCoordinatesToFilenames;
                    setupData.ImageHeightScale = imageHeightScale;
                    setupData.MinimumRegionDataPoints = _minimumRegionDataPoints;
                    setupData.OutputSVGFormat = _outputSVGFormat;
					// convert to data points
					setupData.MinimumPointDelta = _minimumVectorOutputPointDelta / _topoData.MetersPerCell();

                    TopoMapGenerator generator = TopoMapGenerator.getGenerator( setupData );
                    generator.DetermineImageDimensions();
                    generator.setColorsDict(colorsDict);
                    generator.addTimingHandler = addTiming;

                    System.Console.WriteLine(ConsoleSectionSeparator);
                    System.Console.WriteLine("Creating map in " + generator.GetName() + " mode.");

                    generator.Generate();

                }   // end if !dataReportOnly

                if ( reportTimings )
                {
                    echoTimings();
                }

                System.Console.WriteLine();
            }   // end if args parsed successfully

            return ReturnSuccess;   // this should maybe be in the else proceed block above, and return an error if execution went here
        }   // end Main()

    }   // end class Program
}   // end namespace


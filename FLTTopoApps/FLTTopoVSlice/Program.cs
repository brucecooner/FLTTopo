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

        // ---- CONSTANTS ----
        const String noInputsErrorMessage = "No inputs.";
        const String noInputFileSpecifiedErrorMessage = "No input file specified.";
        const String moreThanOneInputFileSpecifiedErrorString = "More than one input file specified.";
        const String ConsoleSectionSeparator = "- - - - - - - - - - -";
        const String BannerMessage = "FLT Topo Data Vertical Slicer (run with '?' for options list)";  // "You wouldn't like me when I'm angry."
        const char HelpRequestChar = '?';
        const String LatitudeString = "Latitude";
        const String LongitudeString = "Longitude";

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

        static float cornerALatitude = floatNotSpecifiedValue;
        static float cornerALongitude = floatNotSpecifiedValue;

        static float cornerBLatitude = floatNotSpecifiedValue;
        static float cornerBLongitude = floatNotSpecifiedValue;

        static float rectWidth = floatNotSpecifiedValue;

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
        static private Boolean parseCoordinate(String str, String coordName, ref float coordinateValue, ref String parseErrorString)
        {
            Boolean parsed = false;

            parsed = float.TryParse(str, out coordinateValue);

            if (!parsed)
            {
                parseErrorString = "could not get " + coordName + " coordinate from string '" + str + "'";
            }

            return parsed;
        }

        // ------------------------------------------------------------
        static private Boolean parseLatLong( String input, ref float latitude, ref float longitude, ref String parseErrorString )
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

            parsed = float.TryParse( input, out rectWidth );

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

        }   // end Main()

    }   // end class Program    
}

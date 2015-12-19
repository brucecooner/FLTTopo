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
 * -make it do what it says
 * -rect input (but how?)
*/

namespace FLTTopoVSlice
{
    class Program
    {
        const float versionNumber = 1.0f;

        // ---- TYPES ----
        // used to specify a left right direction
        enum DirectionType
        {
            Left,
            Right
        };

        // different options the user specifies
        enum OptionType
        {
            HelpRequest,        // list options
            OutputFile,         // name of output file
            CornerA,            // one end of baseline
            CornerB,            // other end of baseline
            Width,              // width (perpendicular to baseline)
            PerpendicularDir,   // (left/right) direction of perpendicular relative to A->B baseline
            BackgroundColor,    // color of 'ground' between contour lines
            ContourColor       // color of contour lines 
        };

        delegate Boolean ParseOptionDelegate(String input, ref String parseErrorString);

        // ============================================================
        class OptionSpecifier
        {
            public String Specifier;          // string, entered in arguments, that denotes option
            public String HelpText;           // shown in help mode
            public String Description;        // used when reporting option values

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

        // ---- CONSTANTS ----
        const String noInputsErrorMessage = "No inputs.";
        const String noInputFileSpecifiedErrorMessage = "No input file specified.";
        const String ConsoleSectionSeparator = "- - - - - - - - - - -";
        const String BannerMessage = "FLT Topo Data Vertical Slicer (run with '?' for options list)";  // "You wouldn't like me when I'm angry."
        const char HelpRequestChar = '?';
        const String LatitudeString = "Latitude";
        const String LongitudeString = "Longitude";

        const float floatNotSpecifiedValue = float.MaxValue;

        // ---- DEFAULTS ----
        const String DefaultOutputFileSuffix = "_vslice";

        const String DefaultBackgroundColorString = "FFFFFF";
        const Int32 DefaultBackgroundColor = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF); // white

        const String DefaultContourColorString = "000000";
        const Int32 DefaultContourColor = (Int32)(((byte)0xFF << 24) | 0); // black

        // list of single line operating notes
        static List<String> programNotes = null;

        // ---- OPTIONS ----
        // supported options
        static Dictionary<OptionType, OptionSpecifier> optionTypeToSpecDict;

        static Dictionary<DirectionType, OptionSpecifier> directionToSpecifierDict;

        // ---- SETTINGS ----
        static String inputFileBaseName = "";
        static String outputFileBaseName = "";

        static bool helpRequested = false;

        static String parseErrorMessage = "";

        static Int32 backgroundColor = DefaultBackgroundColor;
        static String backgroundColorString = DefaultBackgroundColorString;

        static Int32 contourColor = DefaultContourColor;
        static String contourColorString = DefaultContourColorString;

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
        // converts hex triplet to 32 bit pixel (note : does NOT expect 0x prefix on string)
        static private Int32 parseColorHexTriplet(String input, ref Boolean parsed, ref String parseErrorString)
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
                String redString = input.Substring(0, 2);
                String greenString = input.Substring(2, 2);
                String blueString = input.Substring(4, 2);

                Int32 redValue = 0;
                Int32 greenValue = 0;
                Int32 blueValue = 0;

                try
                {
                    redValue = Convert.ToInt32(redString, 16);
                }
                catch (System.FormatException)
                {
                    parsed = false;
                    parseErrorString = "could not convert '" + redString + "' to red value";
                }

                try
                {
                    greenValue = Convert.ToInt32(greenString, 16);
                    blueValue = Convert.ToInt32(blueString, 16);
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
        static private Boolean parseColor(String input, String colorDescription, ref String parseErrorString, ref Int32 colorOut, ref String colorStringOut)
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
        static private Boolean parseBackgroundColor(String input, ref String parseErrorString)
        {
            return parseColor(input, optionTypeToSpecDict[OptionType.BackgroundColor].Description, ref parseErrorString, ref backgroundColor, ref backgroundColorString);
        }

        // ------------------------------------------------------
        static private Boolean parseContourColor(String input, ref String parseErrorString)
        {
            return parseColor(input, optionTypeToSpecDict[OptionType.ContourColor].Description, ref parseErrorString, ref contourColor, ref contourColorString);
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
            directionToSpecifierDict = new Dictionary<DirectionType, OptionSpecifier>(Enum.GetNames(typeof(DirectionType)).Length);

            // not crazy about these casts, will see how it works in practice
            // TODO : more descriptive help text?
            directionToSpecifierDict.Add(DirectionType.Left, new OptionSpecifier
            {
                Specifier = (DirectionType.Left).ToString(),
                Description = "Left",
                HelpText = ""
            });
            directionToSpecifierDict.Add(DirectionType.Right, new OptionSpecifier
            {
                Specifier = (DirectionType.Right).ToString(),
                Description = "Right",
                HelpText = ""
            });

            optionTypeToSpecDict = new Dictionary<OptionType, OptionSpecifier>(Enum.GetNames(typeof(OptionType)).Length);

            optionTypeToSpecDict.Add(OptionType.HelpRequest, new OptionSpecifier
            {
                Specifier = HelpRequestChar.ToString(),
                Description = "Help (list available options)",
                HelpText = ": print all available parameters",
                ParseDelegate = parseHelpRequest
            });
            optionTypeToSpecDict.Add(OptionType.OutputFile, new OptionSpecifier
            {
                Specifier = "outfilebase",
                Description = "Output file base name",
                HelpText = "<OutputFile>: specifies output image file base name",
                ParseDelegate = parseOutputFile
            });
            optionTypeToSpecDict.Add(OptionType.BackgroundColor, new OptionSpecifier
            {
                Specifier = "bgcolor",
                Description = "Background color",
                HelpText = "<RRGGBB>: Background Color (defaults to white)",
                ParseDelegate = parseBackgroundColor
            });
            optionTypeToSpecDict.Add(OptionType.ContourColor, new OptionSpecifier
            {
                Specifier = "color",
                Description = "Ground lines color",
                HelpText = "<RRGGBB>: color of ground level lines in normal mode",
                ParseDelegate = parseContourColor
            });
            optionTypeToSpecDict.Add(OptionType.CornerA, new OptionSpecifier
            {
                Specifier = "cornerA",
                Description = "Corner A",
                HelpText = "<lat,long>: Beginning of baseline forming one edge of rect",
                ParseDelegate = parseCornerACoordinate
            });
            optionTypeToSpecDict.Add(OptionType.CornerB, new OptionSpecifier
            {
                Specifier = "cornerB",
                Description = "Corner B",
                HelpText = "<lat,long>: End of baseline forming one edge of rect",
                ParseDelegate = parseCornerBCoordinate
            });
            optionTypeToSpecDict.Add( OptionType.PerpendicularDir, new OptionSpecifier
            {
                Specifier = "perpDir",
                Description = "Perpendicular Direction",
                HelpText = ": Direction of perpendicular to edge specified by CornerA->CornerB",
                AllowedValues = directionToSpecifierDict.Values.ToList<OptionSpecifier>()
            });
            optionTypeToSpecDict.Add(OptionType.Width, new OptionSpecifier
            {
                Specifier = "width",
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
        static private Boolean parseArgs(string[] args)
        {
            Boolean parsed = true;

            if (0 == args.Length)
            {
                parseErrorMessage = noInputsErrorMessage;
                helpRequested = true;
                parsed = false;
            }
            else
            {
                foreach (string currentArg in args)
                {
                    if ('-' == currentArg.ElementAt(0))
                    {
                        String currentOptionString = currentArg.Substring(1); // skip dash
                        Boolean matched = false;

                        // try to match an option specifier
                        foreach (var currentOptionSpec in optionTypeToSpecDict.Values)
                        {
                            if (currentOptionString.StartsWith(currentOptionSpec.Specifier))
                            {
                                matched = true;

                                // skip specifier string
                                currentOptionString = currentOptionString.Substring(currentOptionSpec.Specifier.Length);

                                parsed = currentOptionSpec.ParseDelegate(currentOptionString, ref parseErrorMessage);
                            }

                            if (matched)
                            {
                                break;
                            }
                        }   // end foreach option spec

                        // detect invalid options
                        if (false == matched)
                        {
                            parseErrorMessage = "Unrecognized option : " + currentArg;
                            parsed = false;
                        }
                    }
                    else // no dash
                    {
                        // I'll specifically support no dash on ye olde help request
                        // requesting help?
                        if (HelpRequestChar == currentArg.ElementAt(0))
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
                    if (false == parsed)
                    {
                        break;
                    }
                }
            }

            // must specify input file
            if (0 == inputFileBaseName.Length)
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

        // -------------------------------------------------------------------------------------
        static private void reportOptionValues()
        {
            Console.WriteLine(ConsoleSectionSeparator);

            Console.WriteLine("Input file base name : " + inputFileBaseName);

            Console.WriteLine(optionTypeToSpecDict[OptionType.OutputFile].Description + " : " + outputFileBaseName);
        }

        // --------------------------------------------------------------------------------------
        static private void reportAvailableOptions()
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
                // Note : hardwiring the dash in front of these optional parameters
                // also note that description string immediately follows specifier
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

            Console.WriteLine("Notes:");
            foreach (var currentNote in programNotes)
            {
                Console.WriteLine(currentNote);
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
                    reportAvailableOptions();
                }
            }

        }   // end Main()

    }   // end class Program    
}

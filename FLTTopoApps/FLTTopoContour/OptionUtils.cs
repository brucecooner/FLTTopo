using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OptionUtils
{
    // callbacks that handle the values given to arguments
    delegate Boolean    ParseOptionDelegate(String input, ref String parseErrorString);

    // callback in cases where arg parser cannot resolve an option
    // note : caller can return an empty error string to indicate option was not recognized in any way
    delegate Boolean    UnknownParameterCallback( String arg, ref String errorMessage );

    static class ParseSupport
    {
        // ------------------------------------------------------
        // converts hex triplet to 32 bit pixel (note : does NOT expect 0x prefix on string)
        static public Int32 parseColorHexTriplet( String input, ref Boolean parsed, ref String parseErrorString )
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

        // -------------------------------------------------------------------------------------------
        static public Boolean ParseArgs(    string[] args,
                                            IList<OptionSpecifier> optionSpecs,
                                            UnknownParameterCallback unknownParameterHandler,
                                            ref String parseErrorMessage)
        {
            Boolean parseSuccessful = true;

            if (0 == args.Length)
            {
                parseSuccessful = false;
                parseErrorMessage = "No options specified.";
            }
            else
            {
                Boolean matched = false;

                foreach (string currentArgString in args)
                {
                    // find matching option spec
                    // try to match an option specifier
                    foreach (var currentOptionSpec in optionSpecs)
                    {
                        matched = false;

                        // have to check lengths, or for an equal sign because sometimes one option specifier is a substring of another,
                        // and will throw a false positive if tested against the longer option
                        if (        (currentArgString.StartsWith(currentOptionSpec.Specifier))
                                &&  (       (currentArgString.Length == currentOptionSpec.Specifier.Length )
                                        ||  (currentArgString.Substring(currentOptionSpec.Specifier.Length).StartsWith("=") ) ) )
                        {
                            matched = true;

                            // skip specifier string
                            String currentArgValue = currentArgString.Substring(currentOptionSpec.Specifier.Length);
                            // skip equal sign if present
                            if (currentArgValue.StartsWith("="))
                            {
                                currentArgValue = currentArgValue.Substring(1);
                            }

                            parseSuccessful = currentOptionSpec.ParseDelegate(currentArgValue, ref parseErrorMessage);
                        }

                        if (matched)
                        {
                            break;
                        }
                    }   // end foreach option spec

                    if (false == matched)
                    {
                        // attempt to resolve with caller
                        parseSuccessful = unknownParameterHandler(currentArgString, ref parseErrorMessage);

                        // if caller failed, but did not return an error, supply one
                        if (false == parseSuccessful && ( 0 == parseErrorMessage.Length ) )
                        {
                            parseErrorMessage = "Unrecognized option : " + currentArgString;
                        }
                    }

                    // break if an arg parse failed
                    if ( false == parseSuccessful )
                    {
                        break;
                    }
                }   // end for each string in args
            } // end else args.Length > 0

            return parseSuccessful;
        } // end parseArgs

        // -------------------------------------------------------------------
        public static String ARGBColorToHexTriplet( Int32 color )
        {
            String triplet = "";

            //        const Int32 DefaultBackgroundColor = (Int32)(((byte)0xFF << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF); // white

            //red
            int redComp = (color >> 16) & 0xFF;

            //green
            int greenComp = (color >> 8) & 0xFF;

            //blue
            int blueComp = (color) & 0xFF;

            triplet = redComp.ToString("X2") + greenComp.ToString("X2") + blueComp.ToString("X2");

            return triplet;
        }

    };  // end class ParseSupport

    // ////////////////////////////////////////////////////////////////////////////////////////
    class OptionSpecifier
    {
        public String Specifier;          // string, entered in arguments, that denotes option
        public String HelpText;           // shown in help mode
        public String Description;        // used when reporting option values

        public Boolean ExpectsValue;       // if true, this option expects a value

        public ParseOptionDelegate ParseDelegate;   // used to translate input text to option value

        // list of (string type only) parameters that can specify values for this parameter
        public List<OptionSpecifier> AllowedValues;

        public OptionSpecifier()
        {
            Specifier = "";
            HelpText = "";
            ParseDelegate = null;
            AllowedValues = null;
            ExpectsValue = false;
        }

    };  // end class OptionSpecifier
}
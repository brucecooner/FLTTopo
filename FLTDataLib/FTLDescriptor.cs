using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

// TODO :
// -extract rest of fields, save in descriptor
// -handle handle upper/lowercase mixed names in header files
// -handle MAJOR offset in Descriptor coordinates after cell is extracted

namespace FLTDataLib
{
    // ===================================
    public class FLTDescriptor
    {
        // ---- constants ----
        public const string NumberOfColumnsKey  = "ncols";
        public const string NumberOfRowsKey     = "nrows";
        public const string XLowerLeftCornerKey = "xllcorner";
        public const string YLowerLeftCornerKey = "yllcorner";
        public const string CellSizeKey         = "cellsize";
        public const string NoDataValueKey      = "NODATA_value";
        public const string ByteOrderKey        = "byteorder";
        
        public const string LSBFirst_Value      = "LSBFIRST";
        public const string MSBFirst_Value      = "MSBFIRST";

        // ---- data ----
        public int NumberOfColumns { get; set; }
        public int NumberOfRows { get; set; }

        public string ByteOrder { get; set; }

        public  float   XLowerLeftCorner { get; set; }
        public  float   YLowerLeftCorner { get; set; }

        // just keep as string (for now since not using for any calculations)
        public string    CellSize { get; set; }

        public int      NoDataValue { get; set; }

        // -----------------------------------------------------------------------------------------
        // ---- constructor ----
        public FLTDescriptor()
        {
            NumberOfColumns = -1;
            NumberOfRows = -1;
        }

        // --------------------------------------------------------------------------------------
        public Boolean IsInitialized()
        {
            Boolean initialized = (NumberOfRows > 0) && (NumberOfColumns > 0);
            return initialized;
        }

        // ----------------------------------------------------------------------------------------
        // note : you pass in the whole file's contents
        string  GetStringValueFromHeaderFile(String Key, String fileContents)
        {
            string value = "";

            int nameBegin = fileContents.IndexOf(Key);

            if (-1 != nameBegin)
            {
                int currentChar = nameBegin + Key.Length;
                // skip spaces
                // TODO : handle eol, eof
                while (fileContents[currentChar ] == ' ') { ++currentChar; }

                while (     ( fileContents[currentChar] != '\n' ) 
                        &&  ( fileContents[currentChar] != '\r' ) )
                {
                    value += fileContents[currentChar];
                    ++currentChar; 
                }
            }

            return value;
        }

        // ----------------------------------------------------------------------------------------
        // note : you pass in the whole file's contents
        float GetFloatValueFromHeaderFile(String Key, String fileContents)
        {
            float value = -1;

            int nameBegin = fileContents.IndexOf(Key);

            if (-1 != nameBegin)
            {
                String numbers = "-.0123456789";

                int valueBegin = fileContents.IndexOfAny(numbers.ToCharArray(), nameBegin + Key.Length);

                if (-1 != valueBegin)
                {
                    string numString = "";

                    // TODO : handle eof
                    while (fileContents[valueBegin] != '\n')
                    {
                        numString += fileContents[valueBegin];
                        ++valueBegin;
                    }

                    if (false == float.TryParse(numString, out value))
                    {
                        value = -1;
                    }
                }
            }

            return value;
        }

        // ----------------------------------------------------------------------------------------
        // note : you pass in the whole file's contents
        int GetIntValueFromHeaderFile( String Key, String fileContents )
        {
            int value = -1;

            // find Name in file
            int nameBegin = fileContents.IndexOf(Key);

            if (-1 != nameBegin)
            {
                String  numbers = "-0123456789";

                int valueBegin = fileContents.IndexOfAny( numbers.ToCharArray(), nameBegin + Key.Length );

                if ( -1 != valueBegin )
                {
                    string numString = "";

                    // TODO : handle eof
                    while (fileContents[valueBegin] != '\n')
                    {
                        numString += fileContents[valueBegin];
                        ++valueBegin;
                    }

                    if (false == int.TryParse( numString, out value))
                    {
                        value = -1;
                    }
                }
            }

            return value;
        }

        // ----------------------------------------------------------------------------------------
        public Boolean ReadFromFile( String headerFileName )
        {
            Boolean success = true;

            // Read the file as one string.
            System.IO.StreamReader headerFile = new System.IO.StreamReader(headerFileName);
            // represents entire header file contents
            string headerString = headerFile.ReadToEnd();

            // parse it out
            NumberOfColumns     = GetIntValueFromHeaderFile( NumberOfColumnsKey,    headerString );
            NumberOfRows        = GetIntValueFromHeaderFile( NumberOfRowsKey,       headerString );
            XLowerLeftCorner    = GetFloatValueFromHeaderFile( XLowerLeftCornerKey, headerString );
            YLowerLeftCorner    = GetFloatValueFromHeaderFile( YLowerLeftCornerKey, headerString );
            CellSize            = GetStringValueFromHeaderFile( CellSizeKey,         headerString );
            NoDataValue         = GetIntValueFromHeaderFile( NoDataValueKey,        headerString );
            ByteOrder           = GetStringValueFromHeaderFile( ByteOrderKey,       headerString );

            headerFile.Close();

            return success;
        }   // end ReadFromFile()

        // ----------------------------------------------------------------------------
        public  async   void    SaveToFile( string  outFileBaseName )
        {
            // TODO : make this line up values
            string  spacer = "          ";

            using (StreamWriter writer = File.CreateText( outFileBaseName + "." + FLTDataLib.Constants.HEADER_FILE_EXTENSION ))
            {
                await writer.WriteLineAsync( NumberOfColumnsKey + spacer + NumberOfColumns );
                await writer.WriteLineAsync( NumberOfRowsKey + spacer + NumberOfRows );
                await writer.WriteLineAsync( XLowerLeftCornerKey + spacer + XLowerLeftCorner );
                await writer.WriteLineAsync( YLowerLeftCornerKey + spacer + YLowerLeftCorner );
                await writer.WriteLineAsync( CellSizeKey + spacer + CellSize );
                await writer.WriteLineAsync( NoDataValueKey + spacer + NoDataValue );
                await writer.WriteLineAsync( ByteOrderKey + spacer + ByteOrder );
            }
        }

        //  ---------------------------------------------------------------------------
        // writes fields to console
        public void Report()
        {
            System.Console.WriteLine( NumberOfColumnsKey + " : " + NumberOfColumns );
            System.Console.WriteLine( NumberOfRowsKey + " : " + NumberOfRows );
            System.Console.WriteLine( XLowerLeftCornerKey + " : " + XLowerLeftCorner );
            System.Console.WriteLine( YLowerLeftCornerKey + " : " + YLowerLeftCorner );
            System.Console.WriteLine( CellSizeKey + " : " + CellSize );
            System.Console.WriteLine( NoDataValueKey + " : " + NoDataValue );
            System.Console.WriteLine( ByteOrderKey + " : " + ByteOrder );
        }
    }   // end class FLTDescriptor
}

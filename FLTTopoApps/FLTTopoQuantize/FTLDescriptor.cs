using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FLTDataLib
{
    // ===================================
    public class FLTDescriptor
    {
        // ---- data ----
        public int NumberOfColumns { get; set; }
        public int NumberOfRows { get; set; }

        public string ByteOrder { get; set; }

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
        int GetValueFromHeaderFile( String Name, String fileContents )
        {
            int value = -1;

            // find Name in file
            int nameBegin = fileContents.IndexOf(Name);

            if (-1 != nameBegin)
            {
                String  numbers = "0123456789";

                int valueBegin = fileContents.IndexOfAny( numbers.ToCharArray(), nameBegin + Name.Length );

                if ( -1 != valueBegin )
                {
                    string numString = "";

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

            /*
            System.Console.Write("header file : " + headerFileName + "\n");
            System.Console.Write("Read " + headerString.Length + " bytes.\n\n");
            System.Console.Write("-------------------------------\n");
            System.Console.Write(headerString);
            System.Console.Write("-------------------------------\n");
            System.Console.Write("\n");
             * */

            // parse it out
            NumberOfColumns = GetValueFromHeaderFile( "ncols", headerString );
            NumberOfRows = GetValueFromHeaderFile( "nrows", headerString );

            headerFile.Close();

            return success;
        }   // end ReadFromFile()
    }   // end class FLTDescriptor
}

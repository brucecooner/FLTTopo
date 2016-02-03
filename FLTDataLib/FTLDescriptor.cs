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

// TODO :
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
        public const string XLowerLeftCornerKey = "xllcorner";  // it's odd to me that the official format uses X and Y to specify this
        public const string YLowerLeftCornerKey = "yllcorner";  // coordinate when Lat and Long would seem more appropriate, given the domain
        public const string CellSizeKey         = "cellsize";
        public const string NoDataValueKey      = "NODATA_value";
        public const string ByteOrderKey        = "byteorder";
        
        public const string LSBFirst_Value      = "LSBFIRST";
        public const string MSBFirst_Value      = "MSBFIRST";

        // ---- data ----
        public int NumberOfColumns { get; set; }
        public int NumberOfRows { get; set; }

        public string ByteOrder { get; set; }

        // longitude/latitude (degrees) of lower left corner of map data
        public  double   XLowerLeftCorner { get; set; }
        public  double   YLowerLeftCorner { get; set; }

        // degrees between map data points
        public double    CellSize { get; set; }

        public int      NoDataValue { get; set; }

        // ---- calculated values ----
        // north/south size of map
        public double HeightDegrees
        {
            get
            {
                if (false == IsInitialized())
                { throw new System.InvalidOperationException("Descriptor not initialized."); }

                return CellSize * NumberOfRows;
            }
        }
        // east/west size of map
        public double WidthDegrees
        {
            get
            {
                if (false == IsInitialized())
                { throw new System.InvalidOperationException("Descriptor not initialized."); }

                return CellSize * NumberOfColumns;
            }
        }

        // coordinates (in degrees) of sides of map
        public double WestLongitude
        {
            get 
            { 
                if ( false == IsInitialized() ) 
                    { throw new System.InvalidOperationException( "Descriptor not initialized." ); }

                return XLowerLeftCorner; 
            }
        }

        public double SouthLatitude
        {
            get 
            { 
                if ( false == IsInitialized() ) 
                    { throw new System.InvalidOperationException( "Descriptor not initialized." ); }

                return YLowerLeftCorner; 
            }
        }

        public double NorthLatitude
        {
            get 
            {
                if ( false == IsInitialized() ) 
                    { throw new System.InvalidOperationException( "Descriptor not initialized." ); }

                return SouthLatitude + HeightDegrees;
            }
        }

        public double EastLongitude
        {
            get
            {
                if (false == IsInitialized())
                { throw new System.InvalidOperationException("Descriptor not initialized."); }

                return WestLongitude + WidthDegrees;    // longitude increases to the east
            }
        }

        public double RowIndexToLatitude( int index )
        {
            double latitudeDegrees;

            if ( IsInitialized() )
            {
                if ( index >= 0 && index < NumberOfRows )
                {
                    latitudeDegrees = NorthLatitude - ( CellSize * index );
                }
                else
                {
                    throw new System.ArgumentOutOfRangeException( "specified row index (" + index + ") not within bounds of map data" );
                }
            }
            else
            {
                throw new System.InvalidOperationException( "descriptor not initialized" );
            }

            return latitudeDegrees;
        }

        public double ColumnIndexToLongitude( int index )
        {
            double longitudeDegrees;

            if (IsInitialized())
            {
                if ( index >= 0 && index < NumberOfColumns )
                {
                    longitudeDegrees = WestLongitude + ( index * CellSize );
                }
                else
                {
                    throw new System.ArgumentOutOfRangeException("specified column index (" + index + ") not within bounds of map data");
                }
            }
            else
            {
                throw new System.InvalidOperationException("descriptor not initialized");
            }

            return longitudeDegrees;
        }

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

        // ---- validation ----

        // --------------------------------------------------------------------------------------------------------
        private Boolean validateIndexWithinBounds(int index, int lowerBound, int upperBound, String boundName, List<String> messages)
        {
            Boolean valid = true;

            if ( index < lowerBound || index > upperBound )
            {
                messages.Add( boundName + " index is not within bounds [" + lowerBound + ".." + upperBound + "]");
                valid = false;
            }
            
            return valid;
        }

        // --------------------------------------------------------------------------------------------------------
        private String generateCoordinateOutsideBoundsMessage(  float invalidDegrees, String invalidCoordinateName,
                                                                String lowerBoundName, String upperBoundName )
        {
            return invalidCoordinateName + "(" + invalidDegrees + ") not in " + lowerBoundName + "/" + upperBoundName + " bounds of map.";
        }

        // --------------------------------------------------------------------------------------------------------
        // validates that all four specified coordinates are within bounds of map, can optionally return error messages if not
        public Boolean ValidateCoordinates( float northDegrees, float westDegrees, float southDegrees, float eastDegrees,
                                            String northName, String westName, String southName, String eastName, // only used if messages!=null
                                            List<String> messages ) // only returned if null
        {
            Boolean westValid = ValidateLongitude( westDegrees );
            if ( false == westValid && null != messages )
            {
                messages.Add( generateCoordinateOutsideBoundsMessage( westDegrees, westName, eastName, westName ) );
            }

            Boolean northValid = ValidateLatitude( northDegrees );
            if (false == westValid && null != messages)
            {
                messages.Add( generateCoordinateOutsideBoundsMessage( northDegrees, northName, southName, northName) );
            }

            Boolean eastValid = ValidateLongitude( eastDegrees );
            if (false == eastValid && null != messages)
            {
                messages.Add(generateCoordinateOutsideBoundsMessage( eastDegrees, westName, eastName, westName));
            }

            Boolean southValid = ValidateLatitude( southDegrees );
            if (false == southValid && null != messages)
            {
                messages.Add( generateCoordinateOutsideBoundsMessage( southDegrees, southName, southName, northName));
            }

            return westValid && northValid && eastValid && southValid;
        }

        // --------------------------------------------------------------------------------------------------------
        // validates the list of coordinates against this descriptor, invalid coordinates are placed in a list for return
        // (so a null return value indicates no invalid coordinates)
        public List< Tuple< double, double > > validateCoordinatesList( List< Tuple<double, double> > coordinates )
        {
            List< Tuple<double,double> > invalidCoordinates = null; //new List< Tuple<double,double> >(10);

            foreach ( Tuple<double,double> coordinatePair in coordinates )
            {
                if (        ( false == ValidateLatitude( coordinatePair.Item1 ) )
                        ||  ( false == ValidateLongitude( coordinatePair.Item2 ) )
                    )
                {
                    if ( null == invalidCoordinates )
                    {
                        invalidCoordinates = new List<Tuple<double,double>>( coordinates.Count );
                    }

                    invalidCoordinates.Add( coordinatePair );
                }
            }

            return invalidCoordinates;
        }
    
        // --------------------------------------------------------------------------------------------------------
        // returns true if specified indices will result in a rectangle contained within the data
        // throws : InvalidOperationException if instance is not initialized
        // notes : also tests for ordering of indices (i.e. left < right, top > bottom)
        public Boolean ValidateRectIndices( int left, int top, int right, int bottom,
                                            String leftName, String topName, String rightName, String bottomName,
                                            out List<String> messages )
        {
            Boolean leftValid = true,
                    rightValid = true,
                    topValid = true,
                    bottomValid = true,
                    allOrdered = true;

            messages = new List<String>( 10 );

            if ( !IsInitialized() )
            {
                throw new System.InvalidOperationException( "FltDescriptor is not initialized" );
            }
            else
            {
                // bounds
                leftValid   = validateIndexWithinBounds( left,  0, NumberOfColumns - 1, leftName,   messages );
                rightValid  = validateIndexWithinBounds( right, 0, NumberOfColumns - 1, rightName,  messages );
                topValid    = validateIndexWithinBounds( top,   0, NumberOfRows - 1,    topName,    messages );
                bottomValid = validateIndexWithinBounds( bottom,0, NumberOfRows - 1,    bottomName, messages );

                if ( left >= right )
                {
                    allOrdered = false;
                    messages.Add( leftName + " index(" + left + ") must be less than " + rightName + " index(" + right + ")" );
                }

                // this is a little odd seeming at first glance, the 'top' of the bounds must be less than the 'bottom'. 
                // This is because the row indexes increase as you go down (i.e. south) in topo space, which is addressed 
                // similary to screen space or rows in a bitmap, where 0,0 is in the upper left corner.
                if (top >= bottom )
                {
                    allOrdered = false;
                    messages.Add( topName + " index(" + top + ") must be less than " + bottomName + " index(" + bottom + ")");
                }
            }

            return leftValid && rightValid && topValid && bottomValid && allOrdered;
        }

        // --------------------------------------------------------------------
        public Boolean ValidateLongitude( double longitudeDegrees )
        {
            Boolean valid = false;

            if ( IsInitialized() )
            {
                // note : assumes decreasing values to the east

                if ( ( WestLongitude < longitudeDegrees ) &&  (longitudeDegrees < EastLongitude) )
                {
                    valid = true;
                }
            }
            else
            {
                throw new System.InvalidOperationException( "Descriptor not initialized" );
            }

            return valid;
        }

        // --------------------------------------------------------------------
        public Boolean ValidateLatitude(double latitudeDegrees)
        {
            Boolean valid = false;

            if (IsInitialized())
            {
                if ( (SouthLatitude < latitudeDegrees ) &&  ( latitudeDegrees < NorthLatitude ) )
                {
                    valid = true;
                }
            }
            else
            {
                throw new System.InvalidOperationException("Descriptor not initialized");
            }

            return valid;
        }

        // --------------------------------------------------------------------
        // ---- conversion ----
        // converts longitude in degrees to a column index 
        public int LongitudeToColumnIndex( double longitudeDegrees )
        {
            int index = 0;

            if ( IsInitialized() )
            {
                if ( ValidateLongitude( longitudeDegrees ) )
                {
                    // how far into map is specified coordinate
                    double fraction = Math.Abs( longitudeDegrees - WestLongitude) / WidthDegrees;

                    fraction = Math.Max(0, Math.Min(1, fraction));  // ensure fraction is 0..1

                    index = (int)(NumberOfColumns * fraction);
                }
            }
            else
            {
                throw new System.InvalidOperationException( "Descriptor not initialized." );
            }

            return index;
        }

        // ---------------------------------------------------------------------------
        public int LatitudeToRowIndex(double latitudeDegrees )
        {
            int index = 0;

            if ( IsInitialized() )
            {
                if ( ValidateLatitude( latitudeDegrees ) )
                {
                    double fraction = (latitudeDegrees - SouthLatitude) / HeightDegrees;

                    fraction = Math.Max(0, Math.Min(1, fraction));  // ensure fraction is 0..1

                    // latitudes increase from 'bottom' to 'top', when viewed on a map (i.e. north=up), but this data is actually stored in the map
                    // beginning at the northernmost latitude, so the map is stored 'upside down' relative to latitude, so subtract the computed
                    // row from the number of rows
                    // Or think about it this way : the 'lower left' corner of the data is in 'map' space, not data space.
                    index = NumberOfRows - (int)(NumberOfRows * fraction);
                }
            }
            else
            {
                throw new System.InvalidOperationException( "Descriptor not initialized." );
            }

            return index;
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
            else
            {
                throw new Exception("Could not find '" + Key + "' key in file.");
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
            else
            {
                throw new Exception("Could not find '" + Key + "' key in file.");
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
            else
            {
                throw new Exception( "Could not find '" + Key + "' key in file." );
            }

            return value;
        }

        // ----------------------------------------------------------------------------------------
        public Boolean ReadFromFile( String headerFileName )
        {
            Boolean success = true;

            try 
            {
                // Read the file as one string.
                System.IO.StreamReader headerFile = new System.IO.StreamReader(headerFileName);
                // represents entire header file contents
                string headerString = headerFile.ReadToEnd();

                // parse it out
                NumberOfColumns     = GetIntValueFromHeaderFile( NumberOfColumnsKey,    headerString );
                NumberOfRows        = GetIntValueFromHeaderFile( NumberOfRowsKey,       headerString );
                XLowerLeftCorner    = GetFloatValueFromHeaderFile( XLowerLeftCornerKey, headerString );
                YLowerLeftCorner    = GetFloatValueFromHeaderFile( YLowerLeftCornerKey, headerString );
                CellSize            = GetFloatValueFromHeaderFile( CellSizeKey,         headerString );
                NoDataValue         = GetIntValueFromHeaderFile( NoDataValueKey,        headerString );
                ByteOrder           = GetStringValueFromHeaderFile( ByteOrderKey,       headerString );

                headerFile.Close();
            }
            catch
            {
                throw;
            }

            return success;
        }   // end ReadFromFile()

        // ----------------------------------------------------------------------------
        // note : untested
        /*
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
         * */

        //  ---------------------------------------------------------------------------
        // generates series of strings
        public List<String> GetValueStrings()
        {
            var valueStrings = new List<String>( 10 );

            valueStrings.Add( NoDataValueKey + " : " + NoDataValue );
            valueStrings.Add( ByteOrderKey + " : " + ByteOrder);
            valueStrings.Add( NumberOfColumnsKey + " : " + NumberOfColumns);
            valueStrings.Add( NumberOfRowsKey + " : " + NumberOfRows);
            valueStrings.Add( XLowerLeftCornerKey + " : " + XLowerLeftCorner);
            valueStrings.Add( YLowerLeftCornerKey + " : " + YLowerLeftCorner);
            valueStrings.Add( CellSizeKey + " : " + CellSize);

            return valueStrings;
        }
    }   // end class FLTDescriptor
}

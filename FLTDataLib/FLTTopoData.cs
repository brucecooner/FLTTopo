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

/* TODO :
 * -ability to load just the header, to validate against stuff in it, then proceed later with the flt file load
 *  -note change of byte order in header when data is flipped?
 * 
 */

namespace FLTDataLib
{
    // representation of usgs topo data stored in an hdr/flt file pair.
    // Notes : 
    //    -Height values are in meters.
    //    -The data is stored starting with the northernmost row of points (latitude), so that it is addressed similarly to image bitmaps in
    //     most graphic systems (i.e. row 0 refers to the topmost row, not the bottom row).
    //    -Don't forget that longitudinal coordinates DECREASE to the west
    public class FLTTopoData
    {
        // -----------------------------------------------
        public Boolean IsInitialized()
        {
            Boolean initialized = (topoDataByteArray != null) && (Descriptor != null) && Descriptor.IsInitialized();
            return initialized;
        }

        // ------------------------------------------------
        // constructor
        public FLTTopoData()
        {
            MinMaxFound = false;

            verbose = true;

            Descriptor = null;

            isQuantized = false;
        }

        // ----------------------------------------------
        // ---- info from header file ----
        public  FLTDescriptor Descriptor { get; set; }

        // ---- info from data file ----
        byte[] topoDataByteArray = null;

        public bool isQuantized { get; private set; }

        public int NumRows
        {
            get { return Descriptor.NumberOfRows; }
        }

        public int NumCols 
        {
            get { return Descriptor.NumberOfColumns; }
        }

        // ----------------------------------------------------------------------
        // todo : parameter validation
        public float ValueAt(int Row, int Column)
        {
            int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * ((Row * Descriptor.NumberOfColumns) + Column);

            return System.BitConverter.ToSingle(topoDataByteArray, startIndex);
        }

        // -----------------------------------------------------------------------
        // TODO : parameter validation
        public void    SetValue( int Row, int Column, float Value )
        {
            int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * ((Row * Descriptor.NumberOfColumns) + Column);

            byte[]  floatBytes = System.BitConverter.GetBytes( Value );

            Array.Copy( floatBytes, 0, topoDataByteArray, startIndex, sizeof( float ) );
        }

        // ----------------------------------------------------------------------
        private void    FlipDataByteOrder()
        {
#if false
            // serial operation
            for (int currentValueIndex = 0; currentValueIndex  < (NumRows() * NumCols()); ++currentValueIndex)
            {
                int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * currentValueIndex;

                byte[]  temp = new byte[ FLTDataLib.Constants.FLT_FLOAT_SIZE ];

                temp[ 0 ] = topoDataByteArray[ startIndex + 3 ];
                temp[ 1 ] = topoDataByteArray[ startIndex + 2 ];
                temp[ 2 ] = topoDataByteArray[ startIndex + 1 ];
                temp[ 3 ] = topoDataByteArray[ startIndex ];

                Array.Copy( temp, 0, topoDataByteArray, startIndex, FLTDataLib.Constants.FLT_FLOAT_SIZE );
            }
#else
            // parallel operation
            for ( int row = 0; row < NumRows; ++row )
            {
                Parallel.For( 0, NumCols, col =>
                    {
                        int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * (row * NumCols + col );

                        byte[] temp = new byte[FLTDataLib.Constants.FLT_FLOAT_SIZE];

                        temp[0] = topoDataByteArray[startIndex + 3];
                        temp[1] = topoDataByteArray[startIndex + 2];
                        temp[2] = topoDataByteArray[startIndex + 1];
                        temp[3] = topoDataByteArray[startIndex];

                        Array.Copy(temp, 0, topoDataByteArray, startIndex, FLTDataLib.Constants.FLT_FLOAT_SIZE);
                    }
                );  // end Parallel.For
            }   // end for row
#endif
        }

        // ----------------------------------------------------------------------
        // note : filename should NOT have an extension
        // throws exceptions
        public void ReadDataFile( String filename )
        {
            String dataFilename = filename + "." + FLTDataLib.Constants.DATA_FILE_EXTENSION;

            Report( "Reading topo data file : " + filename + "\n" );

            // cannot load data if descriptor hasn't been loaded
            if ( null == Descriptor )
            {
                throw new System.InvalidOperationException( "Descriptor not initialized." );
            }
            else
            {
                try
                {
                    System.IO.FileStream file = new System.IO.FileStream( dataFilename, System.IO.FileMode.Open);

                    System.IO.BinaryReader binaryFile = new System.IO.BinaryReader(file);

                    topoDataByteArray = binaryFile.ReadBytes( FLTDataLib.Constants.FLT_FLOAT_SIZE * Descriptor.NumberOfColumns * Descriptor.NumberOfRows );

                    Report("  Topo Data read. Length : " + topoDataByteArray.Length + " bytes.\n");

                    string  expectedByteString = ( System.BitConverter.IsLittleEndian ) ? FLTDescriptor.LSBFirst_Value : FLTDescriptor.MSBFirst_Value;

                    if ( false == String.Equals(Descriptor.ByteOrder, expectedByteString, StringComparison.OrdinalIgnoreCase ) )
                    {
                        System.Console.WriteLine( "  Expected byte order : " + expectedByteString + " flt byte order : " + Descriptor.ByteOrder );
                        Report( "  Reversing data byte order.\n" );
                        // flip the data
                        FlipDataByteOrder();
                    }
                }
                catch (System.IO.FileNotFoundException error)
                {
                    Report("ERROR : Could not find data file : " + dataFilename + "\n");

                    throw error;
                }
                catch
                {
                    throw;
                }
            }
        }

        // ---------------------------------------------------------------------
        // fills descriptor data from specified file
        // note : filename should NOT have an extension
        // TODO : validate that all necessary keys are present in file
        public void ReadHeaderFile(String filename)
        {
            String headerFileName = filename + "." + FLTDataLib.Constants.HEADER_FILE_EXTENSION;

            Report("Reading header file : " + headerFileName + "\n");

            Descriptor = new FLTDescriptor();

            try
            {
                Descriptor.ReadFromFile(headerFileName);
            }
            catch (System.IO.FileNotFoundException error)
            {
                Report("Error : Could not find descriptor file : " + headerFileName + "\n");

                throw error;
            }
            catch
            {
                throw;
            }
        }

        // ---------------------------------------------------------------------
        public  void ReadFromFiles( String filenameWithoutExtension )
        {
            ReadHeaderFile( filenameWithoutExtension );
            ReadDataFile( filenameWithoutExtension );
        }   // end ReadFromFiles()

        // --------------------------------------------------------------------------------------------
        // bits of data that might be useful (but aren't necessarily initialized)
        public Boolean MinMaxFound { get; private set; }

        public  float MaximumElevation;
        public  float MinimumElevation;

        // coordinates of max,min
        public  int MaxElevationRow;
        public  int MaxElevationCol;

        public  int MinElevationRow;
        public  int MinElevationCol;

        // -------------------------------------------------------------------------------------------
        // returns minimum and maximum elevation values in specified rect
        // note : out of order coordinates will be reversed
        public void FindMinMaxInRect(   int x1, int y1, int x2, int y2, 
                                        ref float min, ref int minRow, ref int minColumn,
                                        ref float max, ref int maxRow, ref int maxColumn )
        {
            // validation urrrrrrgh
            if (!IsInitialized())
            {
                throw new System.InvalidOperationException( "Topo data has not been initialized." );
            }

            // todo : use descriptor's new validator function to do this!
            if ((x1 < 0) || (x1 >= NumCols)
                    || (x2 < 0) || (x2 >= NumCols)
                    || (y1 < 0) || (y1 >= NumRows)
                    || (y2 < 0) || (y2 >= NumRows))
            {
                throw new ArgumentOutOfRangeException("A coordinate (" + x1 + "," + y1 + "," + x2 + "," + y2 + ") was outside the bounds of the topo map dimensions (0,0," + (NumCols-1) + "," + (NumRows-1) + ")");
            }

            //reverse needed?
            if ( x1 > x2 )
            {
                int temp = x1;
                x1 = x2;
                x2 = temp;
            }
            if ( y2 < y1 )
            {
                int temp = y1;
                y1 = y2;
                y2 = temp;
            }

            max = float.MinValue;
            min = float.MaxValue;

            for (int currentRow = y1; currentRow <= y2; ++currentRow)
            {
                for (int currentColumn = x1; currentColumn <= x2; ++currentColumn)
                {
                    int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * ((currentRow * Descriptor.NumberOfColumns) + currentColumn);

                    float currentValue = System.BitConverter.ToSingle(topoDataByteArray, startIndex);

                    if ( currentValue > max )
                    {
                        max = currentValue;
                        maxRow = currentRow;
                        maxColumn = currentColumn;
                    }

                    if (currentValue < min)
                    {
                        min = currentValue;
                        minRow = currentRow;
                        minColumn = currentColumn;
                    }
                }
            }
        }

        // -------------------------------------------------------------------------------------------
        // finds min/max in entire map, skips if already found unless forceSearch is true
        // note : -records locations of min/max elevations
        // -detects previously discovered min/max
        public void FindMinMax(Boolean forceSearch = false)
        {
            if ( false == MinMaxFound || forceSearch )
            {
                FindMinMaxInRect(   0, 0, NumRows, NumCols, 
                                    ref MinimumElevation, ref MinElevationRow, ref MinElevationCol,
                                    ref MaximumElevation, ref MaxElevationRow, ref MaxElevationCol );
                MinMaxFound = true;
            }
        }

        // ---------------------------------------------------------------------------
        // turns topo data into quantized equivalent, with heights grouped by quantizeStep
        public  void    Quantize( int quantizeStep )
        {
#if false
            // serial operation
            for (int row = 0; row < NumRows(); ++row)
            {
                for (int col = 0; col < NumCols(); ++col)
                {
                    float   currentValue = ValueAt( row, col );

                    currentValue -= (currentValue % quantizeStep);

                    SetValue( row, col, currentValue );
                }
            }
#else
            /*
            // parallel scheme 1
            for (int row = 0; row < NumRows(); ++row)
            {
                Parallel.For( 0, NumCols(), col =>
                {
                    float currentValue = ValueAt(row, col);

                    currentValue -= (currentValue % quantizeStep);

                    SetValue(row, col, currentValue);
                }
                );  // end Parallel.For col
            } // end for row
             */

            // parallel scheme 2, no noticeable timing difference
            Parallel.For( 0, NumRows * NumCols, currentValueIndex =>
                {
                    int    dataIndex = currentValueIndex * FLTDataLib.Constants.FLT_FLOAT_SIZE;

                    float   currentValue = System.BitConverter.ToSingle( topoDataByteArray, dataIndex );

                    float currentQuantizedValue = currentValue - ( currentValue % quantizeStep );

                    byte[] floatBytes = System.BitConverter.GetBytes( currentQuantizedValue );

                    Array.Copy(floatBytes, 0, topoDataByteArray, dataIndex, sizeof(float));
                } );  // end Parallel.For currentValueIndex
#endif
            isQuantized = true;
        }

        // ----------------------------------------------
        // ---- feedback ----
        public  Boolean verbose { get; set; }
        private void Report(String message)
        {
            if (verbose)
            {
                System.Console.Write(message);
            }
        }

    }   // end class FLTTopoData
}

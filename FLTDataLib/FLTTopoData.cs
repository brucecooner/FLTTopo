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
 *  -change byte order in header when data is flipped!
 * 
 */

namespace FLTDataLib
{
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
        }

        // ----------------------------------------------
        // ---- info from header file ----
        public  FLTDescriptor Descriptor { get; set; }

        // ---- info from data file ----
        byte[] topoDataByteArray = null;

        public int NumRows() { return Descriptor.NumberOfRows; }
        public int NumCols() { return Descriptor.NumberOfColumns; }

        // ----------------------------------------------------------------------
        // todo : parameter validation
        public float ValueAt(int Row, int Column)
        {
            int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * ((Row * Descriptor.NumberOfColumns) + Column);

            return System.BitConverter.ToSingle(topoDataByteArray, startIndex);
        }

        // -----------------------------------------------------------------------
        // TODO : parameter validation
        private void    SetValue( int Row, int Column, float Value )
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
            for ( int row = 0; row < NumRows(); ++row )
            {
                Parallel.For( 0, NumCols(), col =>
                    {
                        int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * (row * NumCols() + col );

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
        private Boolean ReadTopoData(String fileName, int NumberOfRows, int NumberOfColumns )
        {
            Boolean success = true;

            Report( "Reading topo data file : " + fileName + "\n" );

            try
            {
                System.IO.FileStream file = new System.IO.FileStream( fileName, System.IO.FileMode.Open);

                System.IO.BinaryReader binaryFile = new System.IO.BinaryReader(file);

                topoDataByteArray = binaryFile.ReadBytes( FLTDataLib.Constants.FLT_FLOAT_SIZE * NumberOfColumns * NumberOfRows );

                Report("Topo Data read. Length : " + topoDataByteArray.Length + " bytes.\n");

                string  expectedByteString = ( System.BitConverter.IsLittleEndian ) ? FLTDescriptor.LSBFirst_Value : FLTDescriptor.MSBFirst_Value;

                if ( false == String.Equals(Descriptor.ByteOrder, expectedByteString, StringComparison.OrdinalIgnoreCase ) )
                {
                    System.Console.WriteLine( "Expected byte order : " + expectedByteString + " flt byte order : " + Descriptor.ByteOrder );
                    Report( "Reversing data byte order.\n" );
                    // flip the data
                    FlipDataByteOrder();
                }
            }
            catch (System.IO.FileNotFoundException error)
            {
                Report("Could not find flt data file : " + fileName + "\n");
                success = false;

                throw error;
            }
            catch
            {
                throw;
            }

            return success;
        }

        // ---------------------------------------------------------------------
        // note : inputFileName should NOT have an extension, there are multiple files with the same name
        public  Boolean ReadFromFiles( String inputFileName )
        {
            Boolean success = true;

            String  headerFileName = inputFileName + "." + FLTDataLib.Constants.HEADER_FILE_EXTENSION;
            String dataFileName = inputFileName + "." + FLTDataLib.Constants.DATA_FILE_EXTENSION;

            Report("Reading header file : " + headerFileName + "\n");

            Descriptor = new FLTDescriptor();
            Boolean headerSuccess = Descriptor.ReadFromFile(headerFileName);

            if (headerSuccess)
            {
                if ( verbose )
                {
                    Descriptor.Report();
                }

                Report( "----\n" );

                try
                {
                    ReadTopoData(dataFileName, Descriptor.NumberOfRows, Descriptor.NumberOfColumns);
                }
                catch
                {
                    throw;
                }
            }
            else
            {
                Report("ERROR : Failure reading from header file : " + headerFileName + "\n\n");
                success = false;
            }

            return success;
        }   // end ReadFromFiles()

        // --------------------------------------------------------------------------------------------
        // bits of data that might be useful (but aren't necessarily initialized)
        public Boolean MinMaxFound { get; private set; }

        public  float MaximumElevation { get; private set; }
        public  float MinimumElevation{ get; private set; }

        // coordinates of max,min
        public  int MaxElevationRow { get; private set; }
        public  int MaxElevationCol { get; private set; }

        public  int MinElevationRow { get; private set; }
        public  int MinElevationCol { get; private set; }

        // -------------------------------------------------------------------------------------------
        // returns minimum and maximum elevation values in specified rect
        // note : out of order coordinates will be reversed
        public void FindMinMaxInRect(int x1, int y1, int x2, int y2, ref float min, ref float max )
        {
            // validation urrrrrrgh
            if (!IsInitialized())
            {
                throw new Exception( "Topo data has not been initialized." );
            }

            if (        ( x1 < 0 ) ||  ( x1 >= NumCols() )
                    ||  ( x2 < 0 ) ||  ( x2 >= NumCols() )
                    ||  ( y1 < 0 ) || ( y1 >= NumRows() )
                    ||  ( y2 < 0 ) || ( y2 >= NumRows() ) )
            {
                throw new ArgumentOutOfRangeException( "A coordinate (" + x1 +","+ y1 +"," + x2+"," + y2+") was outside the bounds of the topo map dimensions (0,0," + NumCols()+","+NumRows()+")" );
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
                    }

                    if (currentValue < min)
                    {
                        min = currentValue;
                    }
                }
            }
        }

        // -------------------------------------------------------------------------------------------
        public void FindMinMax()
        {
            if (IsInitialized())
            {
                MaximumElevation = float.MinValue;
                MinimumElevation = float.MaxValue;

                for (int currentRow = 0; currentRow < Descriptor.NumberOfRows; ++currentRow )
                {
                    for (int currentColumn = 0; currentColumn < Descriptor.NumberOfColumns; ++currentColumn)
                    {
                        int startIndex = FLTDataLib.Constants.FLT_FLOAT_SIZE * ((currentRow * Descriptor.NumberOfColumns) + currentColumn);

                        float currentValue = System.BitConverter.ToSingle( topoDataByteArray, startIndex );

                        if (currentValue > MaximumElevation)
                        {
                            MaximumElevation = currentValue;

                            MaxElevationRow = currentRow;
                            MaxElevationCol = currentColumn;
                        }

                        if (currentValue < MinimumElevation)
                        {
                            MinimumElevation = currentValue;

                            MinElevationRow = currentRow;
                            MinElevationCol = currentColumn;
                        }
                    }
                }

                MinMaxFound = true;
            }
            else
            {
                throw new Exception( "topo data not yet initialized" );
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

            // parallel scheme 2, no noticeable timing difference, both must be maxing out the cores
            Parallel.For( 0, NumRows() * NumCols(), currentValueIndex =>
                {
                    int    dataIndex = currentValueIndex * FLTDataLib.Constants.FLT_FLOAT_SIZE;

                    float   currentValue = System.BitConverter.ToSingle( topoDataByteArray, dataIndex );

                    float currentQuantizedValue = currentValue - ( currentValue % quantizeStep );

                    byte[] floatBytes = System.BitConverter.GetBytes( currentQuantizedValue );

                    Array.Copy(floatBytes, 0, topoDataByteArray, dataIndex, sizeof(float));
                }
            );  // end Parallel.For currentValueIndex
#endif
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

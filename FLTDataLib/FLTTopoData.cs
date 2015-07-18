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

                    currentValue -= ( currentValue % quantizeStep );

                    byte[] floatBytes = System.BitConverter.GetBytes( currentValue );

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

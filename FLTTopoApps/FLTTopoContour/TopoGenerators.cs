using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FLTDataLib;


// classes which (via common interface) generate topo map data in various forms
namespace FLTTopoContour
{
    // base class
    public abstract class TopoMapGenerator
	{
        public enum MapType
        {
            Normal,
            AlternatingColors,
            Gradient,
            HorizontalSlice
        };

        // abstract to get name of generator
        public abstract String GetName();

        // the topo data (NOTE : MAY BE CHANGED BY GENERATORS)
        protected FLTTopoData _data = null;

        protected int _contourHeights;

        // note : derived classes can declare their color names according to need
        protected Dictionary<String, Int32> _colorsDict = new Dictionary<String, Int32>( 10 );

        public enum colorType
        {
            concolor,
            bgcolor,
            altcolor1,
            altcolor2,
            gradlocolor,
            gradhicolor
        }

        // ---------------------------------------------------------
        public void setColor( String colorName, Int32 colorValue )
        {
            if ( _colorsDict.ContainsKey( colorName ) )
            {
                _colorsDict[ colorName ] = colorValue;
            }
            else
            {
                _colorsDict.Add( colorName, colorValue );
            }
        }

        // ------------------------------------------------------
        public void setColorsDict( Dictionary<String,Int32> newDict )
        {
            _colorsDict = newDict;
        }

        // color helpers
        protected Int32 _backgroundColor    { get { return _colorsDict[colorType.bgcolor.ToString()]; } }
        protected Int32 _contourLineColor   { get { return _colorsDict[colorType.concolor.ToString()]; } }
        protected Int32 _color1             { get { return _colorsDict[colorType.altcolor1.ToString()]; } }
        protected Int32 _color2             { get { return _colorsDict[colorType.altcolor2.ToString()]; } }
        protected Int32 _lowColor           { get { return _colorsDict[colorType.gradlocolor.ToString()]; } }
        protected Int32 _highColor          { get { return _colorsDict[colorType.gradhicolor.ToString()]; } }

        // ---- factory function ----
        public static TopoMapGenerator getGenerator(TopoMapGenerator.MapType type, int contourHeights, FLTTopoData data, String outputFilename, int[] rectExtents)
        {
            TopoMapGenerator generator = null;

            switch ( type )
            {
                case MapType.Normal :
                    generator = new NormalTopoMapGenerator(data, contourHeights, outputFilename, rectExtents);
                    break;
                case MapType.Gradient :
                    generator = new GradientTopoMapGenerator(data, contourHeights, outputFilename, rectExtents);
                    break;
                case MapType.AlternatingColors :
                    generator = new AlternatingColorContourMapGenerator(data, contourHeights, outputFilename, rectExtents);
                    break;
                case MapType.HorizontalSlice :
                    generator = new HorizontalSlicesTopoMapGenerator(data, contourHeights, outputFilename, rectExtents);
                    break;
                default:
                    throw new System.InvalidOperationException("unknown OutputModeType : " + type.ToString());
            }

            return generator;
        }

        // ---- output file(s) ----
        protected String _outputFilename;

        // ---- rect extents ----
        // consts to tell how to pack extents into an array
        public const int RectTopIndex = 0;
        public const int RectLeftIndex = 1;
        public const int RectBottomIndex = 2;
        public const int RectRightIndex = 3;

        private int[] _rectIndices;

        // helpers for indices
        protected int rectLeft { get { return _rectIndices[RectLeftIndex]; } }
        protected int rectTop { get { return _rectIndices[RectTopIndex]; } }
        protected int rectRight { get { return _rectIndices[RectRightIndex]; } }
        protected int rectBottom { get { return _rectIndices[RectBottomIndex]; } }

        // ---- timing ----
        // function delegate to use when timings are desired from internal ops
        public delegate void addTimingDelegate(String entryName, float entryTimingMS);

        // set to get timing info out of generators
        public addTimingDelegate addTimingHandler
        {
            get;
            set;
        }

        // -------------------------------------------------------------------------
        protected void addTiming( String timingEntryName, float timingEntryValue )
        {
            if ( null != addTimingHandler )
            {
                addTimingHandler( timingEntryName, timingEntryValue );
            }
        }

        // ---- utility ----
        // ------------------------------------------------------------------------------------
        // returns how many pixels will be in output image (does not account for pixel size)
        protected int outputImagePixelCount()
        {
            //int testsize = ( rectRightIndex - rectLeftIndex + 1 ) * ( rectBottomIndex - rectTopIndex + 1 );

            return ( rectRight - rectLeft + 1) * (rectBottom - rectTop + 1);
        }

        // --------------------------------------------------------------------------------
        protected int outputImageWidth()
        {
            return rectRight - rectLeft + 1;
        }

        // --------------------------------------------------------------------------------
        protected int outputImageHeight()
        {
            return rectBottom - rectTop + 1;
        }

        // ------------------------------------------------------
        // ---- constructor ----
        // note : as the generators are pixel focused, we'll only work with indices into the topo data (for now)
        public TopoMapGenerator(    FLTTopoData data,
                                    int contourHeights,
                                    String outputFilename,
                                    int[] rectIndices
                                )
        {
            _data = data;

            _contourHeights = contourHeights;

            _outputFilename = outputFilename;

            if ( rectIndices.Length < 4 )
            {
                throw new System.InvalidOperationException( "Expected four indices, got : " + rectIndices.Length );
            }
            else
            {
                _rectIndices = rectIndices;
            }

            addTimingHandler = null;
        }

        // ---------------------------------------------------------------------------------------------
        protected void SaveBitmap( String outputFile, Int32[] pixels )
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            int width = outputImageWidth();
            int height = outputImageHeight();

            Bitmap bmp = new Bitmap(outputImageWidth(), outputImageHeight(), System.Drawing.Imaging.PixelFormat.Format32bppRgb);

            System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                IntPtr pointer = handle.AddrOfPinnedObject();

                //Lock all pixels
                System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(
                           new Rectangle(0, 0, bmp.Width, bmp.Height),
                           System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);

                //set the beginning of pixel data
                //bmpData.Scan0 = pointer;
                //System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, topoData.NumCols * topoData.NumRows());
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, outputImagePixelCount());

                //Unlock the pixels
                bmp.UnlockBits(bmpData);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

            // write out bitmap
            bmp.Save( outputFile + ".bmp");
            bmp.Dispose();

            stopwatch.Stop();
            addTiming( "save bitmap: " + outputFile, stopwatch.ElapsedMilliseconds );

        }

        // ------------------------------------------------------------------------
        // the generator function (descendants override this to produce specific types of maps)
        public abstract void Generate();
	}

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class NormalTopoMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "normal"; }

        public NormalTopoMapGenerator(  FLTTopoData data,
                                        int contourHeights,
                                        String outputFilename,
                                        int[] rectIndices
                                        ) : base( data, contourHeights, outputFilename, rectIndices )
        {}

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize( _contourHeights );

            stopwatch.Stop();
            addTiming( "quantization", stopwatch.ElapsedMilliseconds );
            stopwatch.Reset();
            stopwatch.Start();
#if false
            // normal, slow way
            Int32 currentPixel = blackPixel;

            // serial operation
            for ( int row = 1; row < topoData.NumRows(); ++row )
            {
                for ( int col = 1; col < topoData.NumCols; ++col )
                {
                    float   currentValue = topoData.ValueAt( row, col );
                    float   aboveValue = topoData.ValueAt( row - 1, col );
                    float   leftValue = topoData.ValueAt( row, col - 1 );

                    currentPixel = ( ( currentValue != leftValue ) || ( currentValue != aboveValue ) ) ? blackPixel : whitePixel;

                    contourMap.SetPixel( col, row, Color.FromArgb( currentPixel ) );
                }
            }
#else
            // need to use byte array for pixels
            Int32[]     pixels = new Int32[ outputImagePixelCount() ];

            // ------------------------------
            // single pixel row computation (all contours same color)
            Func<int, int>ComputePixelRowSingleColorContours = ( row ) =>
            {
                float   leftValue = _data.ValueAt( row, rectLeft );

                // index to first pixel in row (in image space)
                int     currentPixelIndex = (row - rectTop) * outputImageWidth();   

                Int32   currentPixel = _backgroundColor;

                for ( int col = rectLeft; col <= rectRight; ++col ) // note : moving in topo space
                {
                    float   aboveValue = _data.ValueAt( row - 1, col );
                    float   currentValue = _data.ValueAt( row, col );

                    currentPixel = ((currentValue != leftValue) || (currentValue != aboveValue)) ? _contourLineColor : _backgroundColor;

                    pixels[ currentPixelIndex ] = currentPixel;

                    ++currentPixelIndex;
                    leftValue = currentValue;
                }

                return row;
            };

            // -----------------------------
            // TODO : sort out the '+1' on the start row. They're there because the compute functions acccess currentRow-1, so you cannot start
            // at row zero. So row zero of the bitmap is blank. Can ignore the +1 if rectTop is >0, otherwise fill the bitmap row 0 or something.
            Parallel.For( rectTop + 1, rectBottom, row =>
            {
                ComputePixelRowSingleColorContours( row );
            } );
#endif

            stopwatch.Stop();
            addTiming( "generating normal map", stopwatch.ElapsedMilliseconds );

            SaveBitmap( _outputFilename, pixels );
        }   // end Generate()
    }

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class AlternatingColorContourMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "alternating color"; }

        public AlternatingColorContourMapGenerator( FLTTopoData data,
                                                    int contourHeights,
                                                    String outputFilename,
                                                    int[] rectIndices
                                                    ) : base( data, contourHeights, outputFilename, rectIndices )
        {}

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize( _contourHeights );

            stopwatch.Stop();
            addTiming( "quantization", stopwatch.ElapsedMilliseconds );
            stopwatch.Reset();
            stopwatch.Start();

            // need to use byte array for pixels
            Int32[]     pixels = new Int32[ outputImagePixelCount() ];

            // ------------------------------
            // single pixel row computation (alternating color of odd/even contours)
            Func<int, int> ComputePixelRowAlternatingColorContours = (row) =>
            {
                float leftValue = _data.ValueAt(row, 0);
                // index to first pixel in row
                int currentPixelIndex = (row - rectTop) * outputImageWidth();

                Int32 currentPixel = _backgroundColor;

                // note : looping in topo map space
                for (int col = rectLeft; col <= rectRight; ++col)
                {
                    float aboveValue = _data.ValueAt(row - 1, col);
                    float currentValue = _data.ValueAt(row, col);

                    bool drawCurrent = ((currentValue != leftValue) || (currentValue != aboveValue)) ? true : false;

                    float highestValue = currentValue;

                    if (aboveValue > highestValue)
                    {
                        highestValue = aboveValue;
                    }
                    if (leftValue > highestValue)
                    {
                        highestValue = leftValue;
                    }

                    if (drawCurrent)
                    {
                        currentPixel = _backgroundColor;

                        Int32 evenOdd = Convert.ToInt32(highestValue / _contourHeights % 2);

                        if (evenOdd <= 0)
                        {
                            currentPixel = _color1;
                        }
                        else
                        {
                            currentPixel = _color2;
                        }
                    }
                    else
                    {
                        currentPixel = _backgroundColor;
                    }

                    pixels[currentPixelIndex] = currentPixel;

                    ++currentPixelIndex;
                    leftValue = currentValue;
                }

                return row;
            };

            // -----------------------------
            // TODO : sort out the '+1' on the start row. They're there because the compute functions acccess currentRow-1, so you cannot start
            // at row zero. So row zero of the bitmap is blank. Can ignore the +1 if rectTop is >0, otherwise fill the bitmap row 0 or something.
            Parallel.For( rectTop + 1, rectBottom, row =>
            {
                ComputePixelRowAlternatingColorContours( row );
            } );

            stopwatch.Stop();
            addTiming( "generating alternating colors map", stopwatch.ElapsedMilliseconds );

            SaveBitmap( _outputFilename, pixels );
        }
    }

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class GradientTopoMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "gradient"; }

        public GradientTopoMapGenerator(    FLTTopoData data,
                                            int contourHeights,
                                            String outputFilename,
                                            int[] rectIndices
                                            ) : base( data, contourHeights, outputFilename, rectIndices )
        {}

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);
            stopwatch.Reset();
            stopwatch.Start();

            // get gradient color components
            int lowRed = (_lowColor >> 16) & 0xFF;
            int lowGreen = (_lowColor >> 8) & 0xFF;
            int lowBlue = _lowColor & 0xFF;

            int highRed = (_highColor >> 16) & 0xFF;
            int highGreen = (_highColor >> 8) & 0xFF;
            int highBlue = _highColor & 0xFF;

            int redRange = highRed - lowRed;
            int greenRange = highGreen - lowGreen;
            int blueRange = highBlue - lowBlue;

            // ---- helper func for converting normalized heights in map to color
            var normalizedHeightToColor = new Func<float, Int32>( height => 
            {
                byte redValue = (byte)(lowRed + (height * redRange));
                byte greenValue = (byte)(lowGreen + (height * greenRange));
                byte blueValue = (byte)(lowBlue + (height * blueRange));

                return (Int32)(((byte)0xFF << 24) | (redValue << 16) | (greenValue << 8) | blueValue);
            });

            // note that this finds the min/max of the quantized data, so will not be the true heights, but that's not important to accurately calculating
            // the range
            float minElevationInRect = 0;
            int minElevationRow = 0, minElevationColumn = 0;

            float maxElevationInRect = 0;
            int maxElevationRow = 0, maxElevationColumn = 0;

            stopwatch.Reset();
            stopwatch.Start();

            _data.FindMinMaxInRect( rectLeft, rectTop, rectRight, rectBottom,
                                    ref minElevationInRect, ref minElevationRow, ref minElevationColumn,
                                    ref maxElevationInRect, ref maxElevationRow, ref maxElevationColumn );

            stopwatch.Stop();
            addTiming("min/max discovery", stopwatch.ElapsedMilliseconds);
            stopwatch.Reset();
            stopwatch.Start();

            float range = maxElevationInRect - minElevationInRect; //topoData.MaximumElevation - topoData.MinimumElevation;
            float oneOverRange = 1.0f / range;

            Int32[] pixels = new Int32[outputImagePixelCount()];

            // generate grayscale bitmap from normalized topo data
            // note : looping in TOPO MAP SPACE
            //for (int row = 0; row < topoData.NumRows; ++row)
            Parallel.For( rectTop, rectTop + outputImageHeight() - 1, row =>      // I think the "to" here should be + 1 the upper bound, docs say it is Exclusive
            {
                // compute offset of this row in OUTPUT IMAGE SPACE
                int offset = (row - rectTop) * outputImageWidth();

                //for (int col = 0; col < topoData.NumCols; ++col)
                for (int col = rectLeft; col <= rectRight; ++col)
                {
                    float normalizedValue = (_data.ValueAt(row, col) - minElevationInRect) * oneOverRange;

                    //bmp.SetPixel(col, row, Color.FromArgb(argb)); // seem to remember this being painfully slow
                    pixels[offset] = normalizedHeightToColor(normalizedValue);// argb;
                    ++offset;
                }
                //}   // end for row
            });    // end parallel.for row

            stopwatch.Stop();
            addTiming( "generating gradient map", stopwatch.ElapsedMilliseconds );

            SaveBitmap( _outputFilename, pixels );           
        }

    }

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class HorizontalSlicesTopoMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "HSlice"; }

        public HorizontalSlicesTopoMapGenerator(    FLTTopoData data,
                                                    int contourHeights,
                                                    String outputFilename,
                                                    int[] rectIndices
                                                    )
            : base(data, contourHeights, outputFilename, rectIndices)
        {}

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);
            stopwatch.Reset();
            stopwatch.Start();

            // find minimum and maximum coordinates generator will iterate between.
            // note that this finds the min/max of the quantized data, so will not be the true heights, but that's not important to accurately calculating
            // the range
            float minElevationInRect = 0;
            int minElevationRow = 0, minElevationColumn = 0;

            float maxElevationInRect = 0;
            int maxElevationRow = 0, maxElevationColumn = 0;

            stopwatch.Reset();
            stopwatch.Start();

            _data.FindMinMaxInRect(rectLeft, rectTop, rectRight, rectBottom,
                                    ref minElevationInRect, ref minElevationRow, ref minElevationColumn,
                                    ref maxElevationInRect, ref maxElevationRow, ref maxElevationColumn);

            // debugging
            Console.WriteLine( "max elevation : " + maxElevationInRect );

            stopwatch.Stop();
            addTiming("min/max discovery", stopwatch.ElapsedMilliseconds);

            Int32[] pixels = new Int32[outputImagePixelCount()];

            // since map data was quantized, all data in the map is already on contours, so we can start
            // at the minimum discovered elevation, plus one 'step', since there won't be steps from any
            // lower contours to the minimum elevation
            float currentContour = minElevationInRect + _contourHeights;

            float difference = maxElevationInRect - minElevationInRect;

            // ----------------
            // single pixel row computation (on a specific contour)
            Func<int, float, int>ComputePixelRowSingleContour = ( row, currentContourRow ) =>
            {
                // This row function moves a three by three 'window' across the data. The middle data point represents the
                // current pixel. If it is higher than any of the neighboring values, that pixel represents a step up
                // from below the current contour onto the current contour.
                var rowAbove = new float[3];
                var rowCenter = new float[3];
                var rowBelow = new float[3];

                // initialize row datas 
                rowAbove[0]     = _data.ValueAt( row - 1, rectLeft - 1);
                rowCenter[0]    = _data.ValueAt( row,     rectLeft - 1);
                rowBelow[0]     = _data.ValueAt( row + 1, rectLeft - 1);

                rowAbove[1]     = _data.ValueAt( row - 1, rectLeft );
                rowCenter[1]    = _data.ValueAt( row,     rectLeft );   // 'middle' value, represents current pixel
                rowBelow[1]     = _data.ValueAt( row + 1, rectLeft );

                rowAbove[2]     = _data.ValueAt( row - 1,   rectLeft + 1 );
                rowCenter[2]    = _data.ValueAt( row,       rectLeft + 1 );
                rowBelow[2]     = _data.ValueAt( row + 1,   rectLeft + 1 );

                // index to first pixel in row (in image space) 
                // note : +1 because the first pixel processed is at index one (not zero)
                int     currentPixelIndex = (row - rectTop) * outputImageWidth() + 1;   

                Int32   currentPixel = _backgroundColor;

                // note that loop stops one short of right edge, this is because the tests look one past the current pixel
                for ( int col = rectLeft; col <= rectRight - 1; ++col ) // note : moving in topo space
                {
                    /* // takes about 0.7 seconds for a 4k by 4k rect */
                    // test that pixel is on current contour, and ANY neighbor is on a lower step
                    currentPixel =      ( rowCenter[1] == currentContourRow )
                                    &&  (       ( rowAbove[0] < rowCenter[1] )
                                            ||  ( rowAbove[1] < rowCenter[1] )
                                            ||  ( rowAbove[2] < rowCenter[1] )
                                            ||  ( rowCenter[0] < rowCenter[1] )
                                            ||  ( rowCenter[2] < rowCenter[1] )
                                            ||  ( rowBelow[0] < rowCenter[1] )
                                            ||  ( rowBelow[1] < rowCenter[1] )
                                            ||  ( rowBelow[2] < rowCenter[1] ) ) ? _contourLineColor : _backgroundColor;

                    // neat looking 'stripes' at current contour with simple equality test
                    //currentPixel = (currentValue == currentContourRow) ? contourLineColor : backgroundColor;

                    pixels[ currentPixelIndex ] = currentPixel;

                    ++currentPixelIndex;

                    // 'shift' current windows to the right
                    rowAbove[0] = rowAbove[1];
                    rowAbove[1] = rowAbove[2];
                    rowAbove[2] = _data.ValueAt( row - 1, col + 1);  // new value moves into window...

                    rowCenter[0] = rowCenter[1];
                    rowCenter[1] = rowCenter[2];
                    rowCenter[2] = _data.ValueAt( row, col + 1 );

                    rowBelow[0] = rowBelow[1];
                    rowBelow[1] = rowBelow[2];
                    rowBelow[2] = _data.ValueAt( row + 1, col + 1 );
                }

                // fix up first pixel in row with whatever was put at second index
                pixels[ (row - rectTop) * outputImageWidth() ] = pixels[ (row - rectTop) * outputImageWidth() + 1];

                return row;
            };
            // -----------------

            // ----------------
            // generate the maps...
            while ( currentContour <= maxElevationInRect )
            {
                Console.WriteLine( " processing contour height: " + currentContour );
                stopwatch.Reset();
                stopwatch.Start();

                // draw map at current contour line
                // note +1 on beginning row , this is because the inner loop looks one above and below the current row
                // note : Parallel.For excludes the final (second) index, so it is safe to specify it as the end index
                Parallel.For(rectTop + 1, rectBottom, row =>
                {
                    ComputePixelRowSingleContour(row, currentContour);
                });

                // copy second row of image to first row, and next to last row to last (fixes up edge lines that could not be calculated)
                int nextToLastRowStartIndex = outputImageWidth() * (outputImageHeight() - 2);
                int lastRowStartIndex = outputImageWidth() * (outputImageHeight() - 1);
                for ( int column = 0; column < outputImageWidth(); ++column )
                {
                    pixels[ column ] = pixels[ outputImageWidth() + column ];
                    pixels[lastRowStartIndex + column] = pixels[nextToLastRowStartIndex + column];
                }

                addTiming("generating map for contour height: " + currentContour, stopwatch.ElapsedMilliseconds);

                String currentFilename = _outputFilename + "_" + Convert.ToInt32( currentContour );
                SaveBitmap( currentFilename, pixels );

                // step up to next contour line
                currentContour += _contourHeights;
            }
        }   // end Generate()
    }   // end class
}
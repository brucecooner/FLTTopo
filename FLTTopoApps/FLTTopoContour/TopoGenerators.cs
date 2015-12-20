using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

using FLTDataLib;


// classes which (via common interface) generate topo map data in various forms
namespace FLTTopoContour
{
    // base class
    public abstract class TopoMapGenerator
	{
        // abstract to get name of generator
        public abstract String GetName();

        // the topo data (NOTE : MAY BE CHANGED BY GENERATORS)
        protected FLTTopoData _data = null;

        protected int _contourHeights;

        // note : derived classes can declare their color names according to need
        protected Dictionary<String, Int32> _colorsDict = new Dictionary<String, Int32>( 10 );

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

            stopwatch.Stop();
            addTiming( "save bitmap", stopwatch.ElapsedMilliseconds );
        }

        // ------------------------------------------------------------------------
        // the generator function (descendants override this to produce specific types of maps)
        public abstract void Generate();
	}

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class NormalTopoMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "normal"; }

        public const String BackgroundColorKey = "backgroundColor";
        public const String ContourLineColorKey = "contourColor";

        // helpers
        private Int32 backgroundColor   { get { return _colorsDict[ BackgroundColorKey ]; } }
        private Int32 contourLineColor  { get { return _colorsDict[ ContourLineColorKey ]; } }

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

                Int32   currentPixel = backgroundColor;

                for ( int col = rectLeft; col <= rectRight; ++col ) // note : moving in topo space
                {
                    float   aboveValue = _data.ValueAt( row - 1, col );
                    float   currentValue = _data.ValueAt( row, col );

                    currentPixel = ((currentValue != leftValue) || (currentValue != aboveValue)) ? contourLineColor : backgroundColor;

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
        }
    }

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class AlternatingColorContourMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "alternating color"; }

        public const String BackgroundColorKey = "backgroundColor";
        public const String Color1Key = "color1";
        public const String Color2Key = "color2";

        // helpers
        private Int32 backgroundColor   { get { return _colorsDict[ BackgroundColorKey ]; } }
        private Int32 color1            { get { return _colorsDict[ Color1Key ]; } }
        private Int32 color2            { get { return _colorsDict[Color2Key]; } }

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

                Int32 currentPixel = backgroundColor;

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
                        currentPixel = backgroundColor;

                        Int32 evenOdd = Convert.ToInt32(highestValue / _contourHeights % 2);

                        if (evenOdd <= 0)
                        {
                            currentPixel = color1;
                        }
                        else
                        {
                            currentPixel = color2;
                        }
                    }
                    else
                    {
                        currentPixel = backgroundColor;
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

        public const String LowColorKey = "gradientLoColor";
        public const String HighColorKey = "gradientHiColor";

        // helpers
        private Int32 LowColor
        {
            get { return _colorsDict[ LowColorKey ]; }
        }
        private Int32 HighColor
        {
            get { return _colorsDict[HighColorKey]; }
        }

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
            int lowRed = (LowColor >> 16) & 0xFF;
            int lowGreen = (LowColor >> 8) & 0xFF;
            int lowBlue = LowColor & 0xFF;

            int highRed = (HighColor >> 16) & 0xFF;
            int highGreen = (HighColor >> 8) & 0xFF;
            int highBlue = HighColor & 0xFF;

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

            _data.FindMinMaxInRect( rectLeft, rectTop, rectRight, rectBottom,
                                    ref minElevationInRect, ref minElevationRow, ref minElevationColumn,
                                    ref maxElevationInRect, ref maxElevationRow, ref maxElevationColumn );

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

}
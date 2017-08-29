using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FLTDataLib;


// classes which (via common interface) generate topo map data in various forms
// Note : doesn't do any validation on input parameters, assumes they have been validated against the data
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
            HorizontalSlice,
            VerticalSliceNS,
            VerticalSliceEW
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

        // ///////////////////////////////////////////////////////////////////////////////////////////////////
        // used when constructing a generator (so I don't have to rewrite ALL the constructors when I add more parameters)
        public class GeneratorSetupData
        {
            public TopoMapGenerator.MapType Type
            { get; set; }
            public int ContourHeights
            { get; set; }
            public FLTTopoData Data
            { get; set; }
            public String OutputFilename
            { get; set; }
            public int[] RectIndices
            { get; set; }
            public  int ImageWidth
            { get; set; }
            public int ImageHeight
            { get; set; }
            public Boolean AppendCoordinatesToFilenames
            { get; set; }
            public float ImageHeightScale
            { get; set; }
            public int MinimumRegionDataPoints
            { get; set; }
        };

        // ---- factory function ----
        public static TopoMapGenerator getGenerator( GeneratorSetupData setupData )
        {
            TopoMapGenerator generator = null;

            switch ( setupData.Type )
            {
                case MapType.Normal :
                    generator = new NormalTopoMapGenerator( setupData );
                    break;
                case MapType.Gradient :
                    generator = new GradientTopoMapGenerator( setupData );
                    break;
                case MapType.AlternatingColors :
                    generator = new AlternatingColorContourMapGenerator( setupData );
                    break;
                case MapType.HorizontalSlice :
                    generator = new HorizontalSlicesTopoMapGenerator( setupData );
                    break;
                case MapType.VerticalSliceNS :
                    generator = new VerticalSlicesTopoMapGenerator( VerticalSlicesTopoMapGenerator.SliceDirectionType.NS, setupData );
                    break;
                case MapType.VerticalSliceEW :
                    generator = new VerticalSlicesTopoMapGenerator( VerticalSlicesTopoMapGenerator.SliceDirectionType.EW, setupData );
                    break;
                default:
                    throw new System.InvalidOperationException("unknown OutputModeType : " + setupData.Type.ToString());
            }

            return generator;
        }

        // ---- output file(s) ----
        protected String _outputFilename;

        // ---- minimum region size ----
        protected int _minimumRegionDataPoints = 0;

        // ---- rect extents ----
        // TODO : consider changing this to use lat/long instead
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

        protected int rectWidth 
        { 
            get { return rectRight - rectLeft + 1; } 
        }
        protected int rectHeight 
        {
            get { return rectBottom - rectTop + 1; }    // remember that bottom > top due to storage (think of data like a bitmap/image)
        }

        // ---- image size ----
        protected int _imageWidth
        { get; set; }

        protected int _imageHeight
        { get; set; }

        public static readonly Int32 ImageDimensionNotSpecifiedValue = Int32.MinValue;
        static public bool ImageDimensionSpecified( int dimension )
        {
            return dimension != ImageDimensionNotSpecifiedValue ? true : false;
        }

        // ---------------------------------------------------------------------
        // calculate image dimensions user did NOT specify
        public virtual void DetermineImageDimensions()
        {
            if (        ( false == ImageDimensionSpecified( _imageHeight ) )
                    &&  ( false == ImageDimensionSpecified( _imageWidth ) ) )
            {
                // neither specified, use dimensions of rect
                _imageWidth = rectWidth;
                _imageHeight = rectHeight;
            }
            else if (       ( ImageDimensionSpecified( _imageHeight ) )
                        &&  ( false == ImageDimensionSpecified( _imageWidth ) ) )
            {
                // height specified, width was not, calculate width from height
                _imageWidth = (int)(_imageHeight * (rectWidth / (float)rectHeight));

            }
            else if (       ( ImageDimensionSpecified( _imageWidth ) )
                        &&  ( false == ImageDimensionSpecified( _imageHeight ) ) )
            {
                // width was specified, calculate height
                _imageHeight = (int)(_imageWidth * (rectHeight / (float)rectWidth));
            }
        }

        // ---- min/max ----
        protected float _minElevationInRect = float.MaxValue;
        protected float _maxElevationInRect = float.MinValue;

        protected int _minElevationRow = 0;
        protected int _minElevationColumn = 0;
        protected int _maxElevationRow = 0;
        protected int _maxElevationColumn = 0;

        // -----------------------------------------------------
        protected void findMinMax()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.FindMinMaxInRect( rectLeft, rectTop, rectRight, rectBottom,
                                    ref _minElevationInRect, ref _minElevationRow, ref _minElevationColumn,
                                    ref _maxElevationInRect, ref _maxElevationRow, ref _maxElevationColumn );

            stopwatch.Stop();
            addTiming("min/max discovery", stopwatch.ElapsedMilliseconds);
        }

        // ---- timing logging ----
        // function delegate to use when timings are desired from internal ops
        public delegate void addTimingDelegate(String entryName, float entryTimingMS);

        // set to get timing info out of generators
        public addTimingDelegate addTimingHandler
        {
            get;
            set;
        }

        // -------------------------------------------------------------------------
        protected void addTiming( String timingEntryName, float timingEntryValueMS )
        {
            if ( null != addTimingHandler )
            {
                addTimingHandler( timingEntryName, timingEntryValueMS );
            }
        }

        // ---- utility ----
        // ------------------------------------------------------------------------------------
        // returns how many pixels will be in output image (does not account for pixel depth)
        protected int outputImagePixelCount()
        {
            //return ( rectRight - rectLeft + 1) * (rectBottom - rectTop + 1);
            return _imageWidth * _imageHeight;
        }

        // --------------------------------------------------------------------------------
        protected int outputImageWidth()
        {
            //return rectRight - rectLeft + 1;
            return _imageWidth;
        }

        // --------------------------------------------------------------------------------
        protected int outputImageHeight()
        {
            //return rectBottom - rectTop + 1;
            return _imageHeight;
        }

        // ---------------------------------------------------------------------------------
        protected FLTTopoContour.FLTDataRegionalizer getTopoDataRegions(FLTTopoData data)
        {
            var regionalizer = new FLTTopoContour.FLTDataRegionalizer(data);

            regionalizer.GenerateRegions();

            return regionalizer;
        }

        // ---------------------------------------------------------------------------------
        // sets all points in specified region in data to height 
        // should live in regionalizer?
        protected void setRegionHeight(FLTDataRegionalizer.Region regionToSet, float newHeight, FLTTopoData data)
        {
            regionToSet.regionValue = newHeight;

            foreach (var currentSpan in regionToSet.spanList)
            {
                for (int x = currentSpan.start; x <= currentSpan.end; x += 1)
                {
                    _data.SetValue(currentSpan.row, x, newHeight);
                }
            }
        }

        // ---------------------------------------------------------------------------------
        protected void processMinimumRegions()
        {
            if (_minimumRegionDataPoints > 0)
            {
                Console.WriteLine("Processing minimum regions.");

                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Reset();
                stopwatch.Start();

                var regionalizer = getTopoDataRegions(_data);

                stopwatch.Stop();
                addTiming("region discovery", stopwatch.ElapsedMilliseconds);

                stopwatch.Reset();
                stopwatch.Start();

#if false
                foreach (var statString in regionalizer.getStats())
                {
                    Console.WriteLine(statString);
                }
#endif

                foreach (var currentRegion in regionalizer.RegionList())
                {
                    if (currentRegion.totalDataPoints <= _minimumRegionDataPoints)
                    {
                        var regionOfMinimumSize = regionalizer.getNeighboringRegionOfMinimumSize(currentRegion, _minimumRegionDataPoints);

                        var newHeight = regionOfMinimumSize.regionValue;

#if false
                        Console.WriteLine("flattening region " + currentRegion.Id + " of " + currentRegion.totalDataPoints + " points");
                        Console.WriteLine("topleft most span at : " + currentRegion.minRowMinColSpan.start + "," + currentRegion.minRowMinColSpan.row);
                        Console.WriteLine("org region height: " + currentRegion.regionValue + " new value: " + newHeight);
#endif

                        setRegionHeight(currentRegion, newHeight, _data);
                    }
                }

                stopwatch.Stop();
                addTiming("removing regions", stopwatch.ElapsedMilliseconds);

#if false
                // double check our work
                Console.WriteLine("====================================================");
                Console.WriteLine("Double checking...");
                regionalizer.GenerateRegions();
                foreach (var statString in regionalizer.getStats())
                {
                    Console.WriteLine(statString);
                }
                foreach (var currentRegion in regionalizer.RegionList())
                {
                    if (currentRegion.totalDataPoints <= _minimumRegionDataPoints)
                    {
                        throw new System.Exception("min region processing failed to reduce region at ");
                    }
                }
#endif
            }
        }

        // ------------------------------------------------------
        // ---- constructor ----
        // note : as the generators are pixel focused, we'll only work with indices into the topo data (for now)
        public TopoMapGenerator( GeneratorSetupData setupData )
        {
        /*
            (    FLTTopoData data,
                                    int contourHeights,
                                    String outputFilename,
                                    int[] rectIndices,
                                    int imageWidth, int imageHeight
                                )
         */
            _data = setupData.Data;

            _contourHeights = setupData.ContourHeights;

            _outputFilename = setupData.OutputFilename;

            if ( setupData.RectIndices.Length < 4 )
            {
                throw new System.InvalidOperationException( "Expected four indices, got : " + setupData.RectIndices.Length );
            }
            else
            {
                _rectIndices = setupData.RectIndices;
            }

            _imageWidth = setupData.ImageWidth;
            _imageHeight = setupData.ImageHeight;

            _minimumRegionDataPoints = setupData.MinimumRegionDataPoints;

            addTimingHandler = null;
        }

        // ---------------------------------------------------------------------------------------------
        protected void SaveBitmap( String outputFile, Int32[] pixels, int forceWidth = 0, int forceHeight = 0 )
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            int width = forceWidth > 0 ? forceWidth : outputImageWidth();
            int height = forceHeight > 0 ? forceHeight : outputImageHeight();

            Bitmap bmp = new Bitmap( width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);

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
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, width * height );//outputImagePixelCount());

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
            addTiming( "save bitmap", stopwatch.ElapsedMilliseconds );

        }

        // ---- image pixels ----
        // note : descendent classes must manage
        protected Int32[] _pixels;

        // ---- used to describe a row of the output image (its location in topo data, and coordinates of previous/next image rows) ----
        private class ImageRowDescriptor
        {
            // it would be friendlier to use these as row indices into the topo data, but I may want to subsample some day
            public double previousLatitude  { get; set; }   // latitude of previous row in data
            public double currentLatitude   { get; set; }   // latitude of current row in data (row being generated)
            public double nextLatitude      { get; set; }   // latitude of next row in data

            public int imageY               { get; set; }   // image y coordinate of row

            public ImageRowDescriptor( int y, double prevLat, double currentLat, double nextLat )
            {
                imageY = y;
                previousLatitude = prevLat;
                currentLatitude = currentLat;
                nextLatitude = nextLat;
            }
        }

        private List<ImageRowDescriptor>    _imageRowDescriptors;

        // ------------------------------------------------------------------------
        // the generator function (descendants override this to produce specific types of maps)
        public abstract void Generate();

        // -----------------------------------------------------------------------------------
        // 'default' form of generate function that just makes a single bitmap
        protected void DefaultGenerate()
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            // need to use byte array for pixels
            _pixels = new Int32[ outputImagePixelCount() ];

            GenerationRowIterator();

            stopwatch.Stop();
            addTiming( "generating " + GetName() + " map", stopwatch.ElapsedMilliseconds );

            // todo : copy next to last image rows to edge rows around image
                /*
                // copy second row of image to first row, and next to last row to last (fixes up edge lines that could not be calculated)
                int nextToLastRowStartIndex = outputImageWidth() * (outputImageHeight() - 2);
                int lastRowStartIndex = outputImageWidth() * (outputImageHeight() - 1);
                for ( int column = 0; column < outputImageWidth(); ++column )
                {
                    pixels[ column ] = pixels[ outputImageWidth() + column ];
                    pixels[lastRowStartIndex + column] = pixels[nextToLastRowStartIndex + column];
                }
                 * */

            SaveBitmap( _outputFilename, _pixels );
        }

        // -----------------------------------------------------------------------------------
        // generalized form of a function that steps over rows in the output image space
        // (well, technically, it's stepping from 1,1 to imageWidth-1, imageHeight-1, since some algorithms use topo points to the left/right/top/bottom
        // of the current sample)
        // and generates a single bitmap from the current generator pixel delegate
        protected void GenerationRowIterator()
        {
            // note : our iterators will move in lat/long coordinate space
            // this sets up a three row 'window' that will move down the rows
            double rectDegreesHeight = (rectBottom - rectTop + 1) * _data.Descriptor.CellSize;

            double latitudeStepPerImageRow = rectDegreesHeight / outputImageHeight();               // latitude step per image row
            double previousImageRowLatitude = _data.Descriptor.RowIndexToLatitude( rectTop );       // starting lat is the first row...
            double currentImageRowLatitude = previousImageRowLatitude - latitudeStepPerImageRow;    // current is actually first row (in topo space)
            double nextImageRowLatitude = currentImageRowLatitude - latitudeStepPerImageRow;        // and next is, er, next

            _imageRowDescriptors = new List<ImageRowDescriptor>( outputImageHeight() - 2 );  // note : don't do top/bottom rows of image

            // generate a list of row descriptors, one for each image row that will be generated
            for ( int i = 1; i < outputImageHeight() - 1; ++i )
            {
                _imageRowDescriptors.Add( new ImageRowDescriptor( i, previousImageRowLatitude, currentImageRowLatitude, nextImageRowLatitude ) );

                // shift the 'window' down
                previousImageRowLatitude = currentImageRowLatitude;
                currentImageRowLatitude = nextImageRowLatitude;
                nextImageRowLatitude -= latitudeStepPerImageRow;
            }

            // parallel loop over row descriptors
            // note : starting at image row 1 (not 0), and using outputImageHeight() to stop because parallel.for is exclusive on halting index
            //for ( int rowDescIndex = 0; rowDescIndex < _imageRowDescriptors.Count; ++rowDescIndex )
            Parallel.For( 0, _imageRowDescriptors.Count, rowDescIndex =>
            {
                GenerationColumnIterator( _imageRowDescriptors[ rowDescIndex ] );
            } );
        }

        // delegate which calculates a single pixel in the output image, receives a 3,3 grid of the samples centered on the image pixel's height sample
        // (note that samples may not be continuous in the source data, depending on the image scale being applied)
        public delegate Int32 PixelCalculationDelegate( float[,] samples );

        // descendent classes can override this, and GenerationColumnIterator() will call it with the 3x3 grid of samples to determine
        // an individual pixel's value
        protected PixelCalculationDelegate _generatorPixelDelegate;

        // -----------------------------------------------------------------------------------
        private void GenerationColumnIterator( ImageRowDescriptor imgRowDesc )
        {
            double rectWidthDegrees = (rectRight - rectLeft + 1) * _data.Descriptor.CellSize;

            // TODO : unify col/row names
            int previousTopoRowIndex = _data.Descriptor.LatitudeToRowIndex( imgRowDesc.previousLatitude );
            int currentTopoRowIndex = _data.Descriptor.LatitudeToRowIndex( imgRowDesc.currentLatitude );
            int nextTopoRowIndex = _data.Descriptor.LatitudeToRowIndex( imgRowDesc.nextLatitude );

            // set up to move a 3 by 3 'window' across the row
            float[,] samples = new float[3,3];

            // could move in indices (integer) space, but might want to interpolate samples someday
            double longitudeStepPerImagePixel = rectWidthDegrees / outputImageWidth();
            double leftLongitude = _data.Descriptor.ColumnIndexToLongitude( rectLeft );
            double currentLongitude = leftLongitude + longitudeStepPerImagePixel;
            double rightLongitude = currentLongitude + longitudeStepPerImagePixel;

            int leftSampleColumnIndex = _data.Descriptor.LongitudeToColumnIndex( leftLongitude );
            int currentSampleColumnIndex = _data.Descriptor.LongitudeToColumnIndex( currentLongitude );
            int rightSampleColumnIndex = _data.Descriptor.LongitudeToColumnIndex( rightLongitude );

            // initialize the samples, centered over image's pixel at 1, imgRowDesc.y

            // previous row
            samples[0,0] = _data.ValueAt( previousTopoRowIndex, leftSampleColumnIndex ); 
            samples[0,1] = _data.ValueAt( previousTopoRowIndex, currentSampleColumnIndex ); 
            samples[0,2] = _data.ValueAt( previousTopoRowIndex, rightSampleColumnIndex );   

            // current row (row pixel is on)
            samples[1,0] = _data.ValueAt( currentTopoRowIndex, leftSampleColumnIndex );
            samples[1,1] = _data.ValueAt( currentTopoRowIndex, currentSampleColumnIndex );
            samples[1,2] = _data.ValueAt( currentTopoRowIndex, rightSampleColumnIndex );

            // next row
            samples[2,0] = _data.ValueAt( nextTopoRowIndex, leftSampleColumnIndex );
            samples[2,1] = _data.ValueAt( nextTopoRowIndex, currentSampleColumnIndex );
            samples[2,2] = _data.ValueAt( nextTopoRowIndex, rightSampleColumnIndex );

            int pixelOffset = (imgRowDesc.imageY * outputImageWidth()) + 1; // start of output row

            for ( int imageX = 1; imageX < outputImageWidth()-1; ++imageX )
            {
                _pixels[ pixelOffset ] = _generatorPixelDelegate( samples );
                ++pixelOffset;  // point to next pixel

                // move the window one pixel to the right (since the were set up already stepping at the rate, simple shifting should work here, and
                // only the right side needs to advance)
                rightLongitude += longitudeStepPerImagePixel;
                rightSampleColumnIndex = _data.Descriptor.LongitudeToColumnIndex( rightLongitude );
                samples[0,0] = samples[0,1];
                samples[0,1] = samples[0,2];
                samples[0,2] = _data.ValueAt( previousTopoRowIndex, rightSampleColumnIndex );

                samples[1,0] = samples[1,1];
                samples[1,1] = samples[1,2];
                samples[1,2] = _data.ValueAt( currentTopoRowIndex, rightSampleColumnIndex );

                samples[2,0] = samples[2,1];
                samples[2,1] = samples[2,2];
                samples[2,2] = _data.ValueAt( nextTopoRowIndex, rightSampleColumnIndex );
            }

        }

        // -----------------------------------------------------------
        // height value positional helpers, gets readings out of heights array based on relative direction 
        protected float heightNW( float[,] heights ) { return heights[0,0]; }
        protected float heightN( float[,] heights ) { return heights[0,1]; }
        protected float heightNE( float[,] heights ) { return heights[0,2]; }

        protected float heightW( float[,] heights ) { return heights[1,0]; }
        protected float heightCurrent( float[,] heights ) { return heights[1,1]; }
        protected float heightE( float[,] heights ) { return heights[1,2]; }

        protected float heightSW( float[,] heights ) { return heights[2,0]; }
        protected float heightS( float[,] heights ) { return heights[2,1]; }
        protected float heightSE( float[,] heights ) { return heights[2,2]; }

	}   // end class TopoMapGenerator

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class NormalTopoMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "contour"; }

        public NormalTopoMapGenerator( GeneratorSetupData setupData ) 
            : base( setupData )
        {}

        // -----------------------------------------------------------------------------------------------
        private Int32 normalTopoMapPixelDelegate( float[,] heights )
        {
            // current != left OR current != above
            return ( heightCurrent( heights ) != heightW(heights) || heightCurrent(heights) != heightN(heights)) ? _contourLineColor : _backgroundColor;
        }

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            _generatorPixelDelegate = normalTopoMapPixelDelegate;

            // -- quantize --
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);
            stopwatch.Reset();
            stopwatch.Start();

            processMinimumRegions();

            // can use default generator 
            DefaultGenerate();
        }
    }

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class AlternatingColorContourMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "alternating color"; }

        public AlternatingColorContourMapGenerator( GeneratorSetupData setupData ) : base( setupData )
        {}

        // -----------------------------------------------------------------------------------------------
        private Int32 alternatingContourColorMapPixelDelegate( float[,] heights )
        {
            Int32 currentPixel = 0;

            bool drawCurrent = ( heightCurrent( heights ) != heightW( heights ) || heightCurrent( heights ) != heightN( heights )) ? true : false;

            float highestValue = heightCurrent( heights );

            highestValue = Math.Max( highestValue, heightN( heights ) );
            highestValue = Math.Max( highestValue, heightW( heights ) );

            if ( drawCurrent )
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

            return currentPixel;
        }

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            // -- quantize --
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);
            stopwatch.Reset();
            stopwatch.Start();

            _generatorPixelDelegate = alternatingContourColorMapPixelDelegate;

            processMinimumRegions();

            // can use default generator
            DefaultGenerate();
        }
    }

    // /////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class GradientTopoMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "gradient"; }

        public GradientTopoMapGenerator( GeneratorSetupData setupData ) : base( setupData )
        {}

        // --- color precalcs ----
        int _lowRed = 0;
        int _lowGreen = 0;
        int _lowBlue = 0;

        int _highRed = 0;
        int _highGreen = 0;
        int _highBlue = 0;

        int _redRange = 0;
        int _greenRange = 0;
        int _blueRange = 0;

        float _oneOverRange = 0;

        // ---- helper func for converting normalized heights in map to color ----
        private Int32 normalizedHeightToColor( float height )
        {
            byte redValue = (byte)(_lowRed + (height * _redRange));
            byte greenValue = (byte)(_lowGreen + (height * _greenRange));
            byte blueValue = (byte)(_lowBlue + (height * _blueRange));

            return (Int32)(((byte)0xFF << 24) | (redValue << 16) | (greenValue << 8) | blueValue);
        }

        // --------------------------------------------------------
        private Int32 gradientTopoMapPixelDelegate( float[,] heights )
        {
            float normalizedValue = ( heightCurrent(heights) - _minElevationInRect) * _oneOverRange;

            return normalizedHeightToColor(normalizedValue);// argb;
        }

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            _generatorPixelDelegate = gradientTopoMapPixelDelegate;

            // -- quantize --
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);
            stopwatch.Reset();
            stopwatch.Start();

            findMinMax();

            float range = _maxElevationInRect - _minElevationInRect; 
            _oneOverRange = 1.0f / range;

            // -- precalcs --
            _lowRed = (_lowColor >> 16) & 0xFF;
            _lowGreen = (_lowColor >> 8) & 0xFF;
            _lowBlue = _lowColor & 0xFF;

            _highRed = (_highColor >> 16) & 0xFF;
            _highGreen = (_highColor >> 8) & 0xFF;
            _highBlue = _highColor & 0xFF;

            _redRange = _highRed - _lowRed;
            _greenRange = _highGreen - _lowGreen;
            _blueRange = _highBlue - _lowBlue;

            processMinimumRegions();

            DefaultGenerate();
        }
    }   // end class GradientTopoMapGenerator

    // ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class HorizontalSlicesTopoMapGenerator : TopoMapGenerator
    {
        public override String GetName() { return "HSlice"; }

        public HorizontalSlicesTopoMapGenerator( GeneratorSetupData setupData )
            : base( setupData )
        {}

        // which horizontal slice is being output on current iteration (needed in pixel delegate)
        float _currentContourHeight;

        // -----------------------------------------------------------------------------------------------
        private Int32 horizontalSlicesPixelDelegate( float[,] heights )
        {
            /* // takes about 0.7 seconds for a 4k by 4k rect */
            // test that pixel is on current contour, and ANY neighbor is on a lower step
            /*
            return              ( rowCenter[1] == currentContourRow )
                            &&  (       ( rowAbove[0] < rowCenter[1] )
                                    ||  ( rowAbove[1] < rowCenter[1] )
                                    ||  ( rowAbove[2] < rowCenter[1] )
                                    ||  ( rowCenter[0] < rowCenter[1] )
                                    ||  ( rowCenter[2] < rowCenter[1] )
                                    ||  ( rowBelow[0] < rowCenter[1] )
                                    ||  ( rowBelow[1] < rowCenter[1] )
                                    ||  ( rowBelow[2] < rowCenter[1] ) ) ? _contourLineColor : _backgroundColor;
             */
            float current = heightCurrent( heights );

            return              ( current == _currentContourHeight )
                            &&  (       (heightNW(heights) < current )
                                    ||  (heightN(heights) < current )
                                    ||  (heightNE(heights) < current )
                                    ||  (heightW(heights) < current )
                                    ||  (heightE(heights) < current )
                                    ||  (heightSW(heights) < current )
                                    ||  (heightS(heights) < current )
                                    ||  (heightSE(heights) < current ) ) ? _contourLineColor : _backgroundColor;
        }

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
            _generatorPixelDelegate = horizontalSlicesPixelDelegate;

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            // -- quantization --
            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);
            stopwatch.Reset();
            stopwatch.Start();

            // -- min/max discovery --
            findMinMax();

            processMinimumRegions();

            // -- not using default generate func, so must manage pixels ourselves --
            _pixels = new Int32[outputImagePixelCount()];

            // since map data was quantized, all data in the map is already on contours, so we can start
            // at the minimum discovered elevation, plus one 'step', since there won't be steps from any
            // lower contours to the minimum elevation
            _currentContourHeight = _minElevationInRect + _contourHeights;

            float difference = _maxElevationInRect - _minElevationInRect;

            // ----------------
            // generate the maps...
            while ( _currentContourHeight <= _maxElevationInRect )
            {
                Console.WriteLine( " processing contour height: " + _currentContourHeight );
                stopwatch.Reset();
                stopwatch.Start();

                GenerationRowIterator();

                stopwatch.Stop();
                addTiming("generating map at contour height: " + _currentContourHeight, stopwatch.ElapsedMilliseconds);

                String currentFilename = _outputFilename + "_" + Convert.ToInt32( _currentContourHeight );
                SaveBitmap( currentFilename, _pixels );

                // step up to next contour line
                _currentContourHeight += _contourHeights;
            }   // end while 
        }

        // -----------------------------------------------------------------------------------------------
#if false
        public void OLDGenerate()
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
#endif
    }   // end class

    // ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static class TupleExtensions
    {
        public static double Latitude( this Tuple<double, double> coord )
        {
            return coord.Item1;
        }

        public static double Longitude( this Tuple<double,double> coord )
        {
            return coord.Item2;
        }

        public static string LLString( this Tuple<double,double> coord )
        {
            return coord.Latitude() + "," + coord.Longitude();
        }
    }

    // ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class VerticalSlicesTopoMapGenerator : TopoMapGenerator
    {
        public enum SliceDirectionType
        {
            NS, // north/south
            EW  // east/west
        }

        public override String GetName() { return "VSlice-" + _sliceDirection.ToString(); }

        // how many meters of buffer to put above and below the ground lines in the vertical slices
        const float BufferMeters = 100.0f;

        // has to be calculated from flt data!
        double _metersPerCell = 0.0; //MetersPerDegree * _data.Descriptor.CellSize; // meters between data points (should be about 10)

        // topo space distance between X coordinates in the picture 
        Tuple<double,double>    _coordinateStepPerImagePixel = null;

        // ---- settings -----
        private SliceDirectionType  _sliceDirection = SliceDirectionType.NS;    

        private Boolean _appendCoordinatesToFilenames = false;

        private float _imageHeightScale = 1.0f;

        public VerticalSlicesTopoMapGenerator( SliceDirectionType sliceDir, GeneratorSetupData setupData )
            : base( setupData )
        {
            _sliceDirection = sliceDir;
            _appendCoordinatesToFilenames = setupData.AppendCoordinatesToFilenames;
            _imageHeightScale = setupData.ImageHeightScale;
        }

        // -----------------------------------------------------------------------------------------------
        override public void DetermineImageDimensions()
        { /* no effect, image dims handled by Generate() after some things have been calculated */ }

        // -----------------------------------------------------------------------------------------------
        //  NOTE : has some dependencies
        private void DetermineImageDimensionsVSlice( SliceDescriptor sampleSlice )
        {
            // note : adding buffer distance below and above min and max elevations 
            // (not needed in ALL slices, but ones that contain min and/or max should be buffered)
            double elevationRange = _maxElevationInRect - _minElevationInRect + (BufferMeters * 2.0f);

            if (        ( false == ImageDimensionSpecified( _imageHeight ) )
                    &&  ( false == ImageDimensionSpecified( _imageWidth ) ) )
            {
                // neither specified...
                // the image "width" is 1x1 with the output rect
                // this means each pixel's real world width is the same a single data point's  (important below)
                _imageWidth = ( SliceDirectionType.NS == _sliceDirection ) ? rectHeight : rectWidth; 

                // the image "height" is the vertical range of readings the slices will cover, plus some buffer at top and bottom
                // pixels, being square, are as tall as they are wide, which is the horizontal distance between data point cells 
                _imageHeight = Convert.ToInt32( elevationRange / _metersPerCell );
            }
            else if (       ( ImageDimensionSpecified( _imageHeight ) )
                        &&  ( false == ImageDimensionSpecified( _imageWidth ) ) )
            {
                // height specified, width was not, calculate width from height
                // distance each pixel represents in specified height
                double metersPerPixel = elevationRange / _imageHeight;

                double sliceWidthDegrees = Vector.Ops.Length( sampleSlice.Start, sampleSlice.End );
                double sliceWidthMeters = sliceWidthDegrees * Constants.Distance.MetersPerDegree;

                _imageWidth = Convert.ToInt32( sliceWidthMeters / metersPerPixel );
            }
            else if (       ( ImageDimensionSpecified( _imageWidth ) )
                        &&  ( false == ImageDimensionSpecified( _imageHeight ) ) )
            {
                // width was specified, calculate height
                // determine scale factor on width (i.e. what is the width of a pixel at this size)
                double sliceWidthDegrees = Vector.Ops.Length( sampleSlice.Start, sampleSlice.End );
                double pixelsPerDegree = _imageWidth / sliceWidthDegrees;
                double pixelsPerMeter = pixelsPerDegree * Constants.Distance.DegreesPerMeter;

                _imageHeight = Convert.ToInt32( elevationRange * pixelsPerMeter );
            }

            // whatever height was, scale it
            _imageHeight = (int)(_imageHeight * _imageHeightScale);
        }

        // -----------------------------------------------------------------------------------------------
        class SliceDescriptor
        {
            public String filename;

            public Tuple<double, double> Start;
            public Tuple<double, double> End;
        }

        // ------------------------------------------------------------------------------------------------
        private Tuple<int,int> CoordinateToRowCol( Tuple<double,double> coordinate )
        {
            return new Tuple<int,int>( _data.Descriptor.LatitudeToRowIndex( coordinate.Latitude() ), _data.Descriptor.LongitudeToColumnIndex( coordinate.Longitude() ) );
        }
        private Tuple<int,int> CoordinateToRowCol( double latitude, double longitude )
        {
            return new Tuple<int,int>( _data.Descriptor.LatitudeToRowIndex( latitude ), _data.Descriptor.LongitudeToColumnIndex( longitude ) );
        }

        // -----------------------------------------------------------------------------------------------
        // receives descriptor and ordinal at which file is being generated in the sequence (keeps files in order)
        private String generateSliceFilename( SliceDescriptor desc, int fileSequenceIndex, bool appendCoordinate )
        {
            String filename ="";

            // -- sequence index --
            // surely 1000 numbers will be enough...for now
            filename = _outputFilename + "_vs" + _sliceDirection.ToString() + "_" + fileSequenceIndex.ToString("D4");

            // -- coordinate --
            if ( appendCoordinate )
            {
                if ( _sliceDirection == SliceDirectionType.NS )
                {
                    filename += "_long_" + desc.Start.Longitude().ToString("F4");
                }
                else
                {
                    filename += "_lat_" + desc.Start.Latitude().ToString("F4");
                }
            }

            return filename;
        }

        // -----------------------------------------------------------------------------------------------
        // iterates over the line between the coordinates start->end, stepping by degreesBetweenSlices, and 
        // generating a slice descriptor at each step that goes from current location to current location + sliceDelta
        private List<SliceDescriptor> generateSliceDescriptors( Tuple<double,double> start, Tuple<double,double> end, 
                                                                double degreesBetweenSlices,
                                                                Tuple<double,double> sliceDelta )
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            // determine how many slices will be generated
            var startEndDelta = Vector.Ops.Delta( start, end );

            // length of delta
            double startEndDeltaDegrees = Vector.Ops.Length( startEndDelta );

            // note : add 1 because the division determines the count of spaces -between- slices
            int numSlices = (int)(startEndDeltaDegrees / degreesBetweenSlices) + 1;

            var slices = new List<SliceDescriptor>( numSlices );

            // need normalized length start->end delta
            var normalizedStartEndDelta = Vector.Ops.Normalize( startEndDelta );

            // now need coincident vector, but sized to degrees step
            var deltaStep = Vector.Ops.Scale( normalizedStartEndDelta, degreesBetweenSlices );

            // starting point of current slice
            double currentStartLatitude = start.Item1;
            double currentStartLongitude = start.Item2;

            for ( int currentSliceIndex = 0; currentSliceIndex < numSlices; ++currentSliceIndex )
            {
                var slice = new SliceDescriptor();

                // -- generate coordinates --
                slice.Start = new Tuple<double, double>( currentStartLatitude, currentStartLongitude );
                slice.End = new Tuple<double,double>( currentStartLatitude + sliceDelta.Latitude(), currentStartLongitude + sliceDelta.Longitude());

                // -- generate filename --
                slice.filename = generateSliceFilename( slice, currentSliceIndex, _appendCoordinatesToFilenames );

                slices.Add( slice );

                currentStartLatitude += deltaStep.Latitude();
                currentStartLongitude += deltaStep.Longitude();
            }

            stopwatch.Stop();
            addTiming( "generate slice descriptors", stopwatch.ElapsedMilliseconds );

            return slices;
        }

        // ---------------------------------------------------------------------------------------------------------
        public override void Generate()
        {
            List<SliceDescriptor> slices = null;

            findMinMax();

            // meters between data points (about 10 for 1/3 arcsecond data)
            _metersPerCell = _data.MetersPerCell();

            double westLongitude = _data.Descriptor.ColumnIndexToLongitude( rectLeft );
            double eastLongitude = _data.Descriptor.ColumnIndexToLongitude( rectRight );
            double northLatitude = _data.Descriptor.RowIndexToLatitude( rectTop );
            double southLatitude = _data.Descriptor.RowIndexToLatitude( rectBottom );

            // -- generate slice descriptors --
            if ( SliceDirectionType.NS == _sliceDirection )
            {
                // go from east to west across rect
                var slicesStart = new Tuple<double,double>( northLatitude, westLongitude );
                var slicesEnd = new Tuple<double,double>( northLatitude, eastLongitude );
                // generating slices from north to south
                var slicesDelta = new Tuple<double,double>( southLatitude - northLatitude, 0 );

                slices = generateSliceDescriptors( slicesStart, slicesEnd, _contourHeights * Constants.Distance.DegreesPerMeter, slicesDelta );
            }
            else // east/west slices
            {
                // go from north to south down rect
                var slicesStart = new Tuple<double,double>( northLatitude, westLongitude );
                var slicesEnd = new Tuple<double,double>( southLatitude, westLongitude );
                var slicesDelta = new Tuple<double,double>( 0, eastLongitude - westLongitude );

                slices = generateSliceDescriptors( slicesStart, slicesEnd, _contourHeights * Constants.Distance.DegreesPerMeter, slicesDelta );
            }

            // -- process slices --
            if ( slices.Count > 0 )
            {
                // now that slices are built, can determine image dimensions
                DetermineImageDimensionsVSlice( slices[0] );
                Console.WriteLine( "Image size(WxH) : " + _imageWidth + " x " + _imageHeight );

                // -- prepare pixel buffer
                _pixels = new Int32[ outputImagePixelCount() ];

                // generate images from slice descriptors
                processSliceDescriptors( slices );
            }
        }

        // -----------------------------------------------------------------------------------------------
        private void processSliceDescriptors( List<SliceDescriptor> slices )
        {
            // -- pre calcs --
            // how far to step in topo coordinate space per image pixel
            var sliceDelta = Vector.Ops.Delta( slices[0].Start, slices[0].End );

            double sliceDegreesWidth = Vector.Ops.Length( sliceDelta );
            // scaling imgWidth over sliceDegreesWidth
            var sliceDeltaNormalized = Vector.Ops.Normalize( sliceDelta );

            _coordinateStepPerImagePixel = Vector.Ops.Scale( sliceDeltaNormalized, sliceDegreesWidth / _imageWidth );

            foreach( SliceDescriptor currentSliceDesc in slices )
            {
                generateSlice( currentSliceDesc );
            }
        }

        // -----------------------------------------------------------------------------------------------
        private void fillPixelBuffer( Int32 color )
        {
            if ( null == _pixels )
            {
                throw new System.InvalidOperationException( "pixel buffer not initialized" );
            }

            for ( int i = 0; i < outputImagePixelCount(); ++i )
            {
                _pixels[i] = color;
            }
        }

        // -----------------------------------------------------------------------------------------------
        // draws vertical line in pixel buffer from x,y1 to x,y2
        private void verticalLine( int x, int y1, int y2, Int32 color )
        {
            // set up to always travel down in y
            int startY = Math.Min( y1, y2 );
            int endY = Math.Max( y1, y2 );

            int offset = startY * outputImageWidth() + x;

            for ( int currentY = startY; currentY <= endY; ++currentY )
            {
                _pixels[ offset ] = color;
                offset += outputImageWidth();   // advance to next lower y
            }
        }

        // -----------------------------------------------------------------------------------------------
        private void generateSlice( SliceDescriptor sliceDesc )
        {
            Console.WriteLine( "generating file = " + sliceDesc.filename );  
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            fillPixelBuffer( _backgroundColor );

            // -- inits --
            double currentLatitude = sliceDesc.Start.Latitude();
            double currentLongitude = sliceDesc.Start.Longitude();

            var startRowCol = CoordinateToRowCol( sliceDesc.Start );
            var endRowCol = CoordinateToRowCol( sliceDesc.End );

            // These are invariant over the slices, why aren't I doing them outside of this?
            // note this is elevation range in image, which includes buffers at top/bottom
            float elevationRange = (_maxElevationInRect - _minElevationInRect) + (BufferMeters * 2.0f);
            // scale elevation range to image width
            float pixelsPerMeter = _imageHeight / elevationRange; 

            var yCoordinates = new int[ _imageWidth ];

            // -- loop over image x axis --
            for ( int currentImageX = 0; currentImageX < _imageWidth; ++currentImageX )
            {
                // what point in data is current image column looking 'down' on ?
                var currentRowCol = CoordinateToRowCol( currentLatitude, currentLongitude );
                var currentHeight = _data.ValueAt( currentRowCol.Item1, currentRowCol.Item2 );

                // the 'y' in the bitmap of the groundline is analogous to the height data at the current point in the topo data (converted to
                // pixels)
                int groundPixelHeight = Convert.ToInt32((currentHeight - _minElevationInRect + BufferMeters) * pixelsPerMeter);

                /*
                 * if this happens, it means my maths are broken again
                if ( _imageHeight - groundPixelHeight < 0 )
                {
                    int breakpoint = 10;
                    breakpoint++;
                }
                 * */

                // note : subtract from image height because bitmap y coordinates are 0 at the top ,increasing downward
                int currentImageY = Math.Max( _imageHeight - groundPixelHeight, 0 );

                yCoordinates[ currentImageX ] = currentImageY;

                _pixels[ (currentImageY * _imageWidth) + currentImageX ] = _contourLineColor;

                // check for vertical gaps between consecutive pixels (>1 pixel apart)
                // It might be better to break up the slice into a series of lines, and handle these gaps that way (by iterating
                // over a series of points, drawing actual lines between them), but for now we'll take advantage of the fact
                // that we're always only moving one pixel horizontally in screen space, and that these gaps don't appear very
                // often in most datasets (unless you're in an area with a lot of cliffs and bluffs e.g. the Grand Canyon)
                if ( currentImageX > 0 )
                {
                    if ( Math.Abs( currentImageY - yCoordinates[ currentImageX - 1 ] ) > 1 )    // found a gap
                    {
                        int midY = (yCoordinates[ currentImageX ] + yCoordinates[ currentImageX - 1 ]) / 2;

                        // fill FROM previous y TO current, but half on each coordinate
                        verticalLine( currentImageX - 1, yCoordinates[ currentImageX - 1 ], midY, _contourLineColor );
                        verticalLine( currentImageX, midY, yCoordinates[ currentImageX ], _contourLineColor );
                    }
                }

                currentLatitude += _coordinateStepPerImagePixel.Latitude();
                currentLongitude += _coordinateStepPerImagePixel.Longitude();
            }

            stopwatch.Stop();
            addTiming( "generate slice", stopwatch.ElapsedMilliseconds );

            SaveBitmap( sliceDesc.filename, _pixels );
        }


    }

}
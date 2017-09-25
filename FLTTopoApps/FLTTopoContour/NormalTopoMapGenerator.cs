using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SVGBuilder;

// TODO:
// -optimize paths
// -detect existing outputfilename extension


namespace FLTTopoContour
{
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
			if (_svgFormat)
			{
				generateSVG();
			}
			else
			{
				generateBMP();
			}
        }

		// -----------------------------------------------------------------------------------------------
		private void generateBMP()
		{
			_generatorPixelDelegate = normalTopoMapPixelDelegate;

			// -- quantize --
			System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Reset();
			stopwatch.Start();

			_data.Quantize(_contourHeights);

			stopwatch.Stop();
			addTiming("quantization",stopwatch.ElapsedMilliseconds);
			stopwatch.Reset();
			stopwatch.Start();

			processMinimumRegions();

			// can use default generator 
			DefaultGenerate();
		}

        // --------------------------------------------------------------
        private void generateSVG()
        {
			var totalPoints = 0;

            // -- quantize --
			var timer = new Timing.Timer(true);

            _data.Quantize(_contourHeights);

            timer.Stop();
            addTiming("quantization", timer.ElapsedMilliseconds);

			// TESTING
			// build a square 'donut' into the topo data
			//_data.SetRegionToHeight(0, 0, 100, 100, 100.0f);
			//_data.SetRegionToHeight(3, 3, 6, 6, 200.0f);
			//_data.SetRegionToHeight(40, 40, 60, 60, 300.0f);

			// build a square with two tabs, top and bottom
			//_data.SetRegionToHeight(0, 0, 100, 100, 100.0f);
			//_data.SetRegionToHeight(20, 20, 80, 80, 200.0f);
			// top tabs
			//_data.SetRegionToHeight(22, 10, 40, 19, 200.0f);
			//_data.SetRegionToHeight(60, 10, 75, 19, 200.0f);
			// bottom tabs
			//_data.SetRegionToHeight(22, 81, 40, 87, 200.0f);
			//_data.SetRegionToHeight(60, 81, 75, 87, 200.0f);

			// TESTING change rect indices
			//this._rectIndices[RectLeftIndex] = 0;
			//this._rectIndices[RectTopIndex] = 0;
			//this._rectIndices[RectRightIndex] = 1000;
			//this._rectIndices[RectBottomIndex] = 1000;

			// ---- regions ----
			timer.ResetAndStart();

			var regionalizerSetup = new FLTDataRegionalizer.RegionalizerSetupData();
			regionalizerSetup.topoData = _data;
			regionalizerSetup.RectIndices = this._rectIndices;

			var regionalizer = new FLTDataRegionalizer(regionalizerSetup);

			regionalizer.GenerateRegions();

			timer.Stop();
			addTiming("region discovery", timer.ElapsedMilliseconds);

			// ---- hulls ----
			Console.WriteLine("Discovering edges");

			timer.ResetAndStart();

			var regionsList = regionalizer.RegionList();

			var pointsListsArray = new List<Tuple<int,int>>[regionsList.Count];

			// parallelization only makes a thousandth of a second difference here, possibly because there's very little math involved
#if TRUE
			Parallel.For(0, regionsList.Count, regionIndex =>
			{
				Boolean includeCurrentRegion = true;

				if (		isMinimumRegionDataPointsSpecified 
						&&	(regionsList[regionIndex].totalDataPoints <= _minimumRegionDataPoints))
				{
					includeCurrentRegion = false;
				}

				if (includeCurrentRegion)
				{
					pointsListsArray[regionIndex] = RegionHullBuilder.getListOfRegionEdgeCoords(regionsList[regionIndex]);
				}
				else
				{
					pointsListsArray[ regionIndex ] = new List<Tuple<int,int>>();
				}
			});
#else
			for (int regionIndex = 0; regionIndex < regionsList.Count; regionIndex += 1)
			{
				Boolean includeCurrentRegion = true;

				if (		isMinimumRegionDataPointsSpecified 
						&&	(regionsList[regionIndex].totalDataPoints <= _minimumRegionDataPoints))
				{
					includeCurrentRegion = false;
				}

				if (includeCurrentRegion)
				{
					pointsListsArray[ regionIndex ] = RegionHullBuilder.getListOfRegionEdgeCoords(regionsList[regionIndex]);
				}	
				else
				{
					pointsListsArray[ regionIndex ] = new List<Tuple<int,int>>();
				}
			}
#endif
			timer.Stop();
			addTiming("edge discovery", timer.ElapsedMilliseconds);

			Console.WriteLine("Generated total of " + totalPoints + " points.");

			Console.WriteLine("Building svg");


			// ---- svg ----
			timer.ResetAndStart();
            var svgBuilder = new SVGBuilder.Builder();

			// move to origin
			svgBuilder.SetTranslate( -rectLeft, -rectTop );
			// size to rect
			svgBuilder.SetWidthAndHeight( rectRight - rectLeft + 1, rectBottom - rectTop + 1);

			foreach (var currentPoints in pointsListsArray)
			{
				svgBuilder.addPath(currentPoints);
			}

            svgBuilder.TestMakingAFile(_outputFilename + ".svg");

			timer.Stop();
			addTiming("svg creation", timer.ElapsedMilliseconds);
        }
	}

}

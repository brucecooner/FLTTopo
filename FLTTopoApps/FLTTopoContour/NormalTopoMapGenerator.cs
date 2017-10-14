using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SVGBuilder;

// TODO:
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
            return ( heightCurrent( heights ) != heightW(heights) || heightCurrent(heights) != heightN(heights)) ? _contourLineColor_RGBA : _backgroundColor_RGBA;
        }

        // -----------------------------------------------------------------------------------------------
        public override void Generate()
        {
			// TODO: create a testing "type" and move this out
#if FALSE
			RunTests();
			return;
#endif

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
			/*
			var pathFilterTest = new FLTTopoContour.PathFilterTest();
			pathFilterTest.TestDistanceCriteria();
			pathFilterTest.TestDistancePlusAngleCriteria();
			return;
			*/
			// END TESTING

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

			Parallel.For(0, regionsList.Count, regionIndex =>
			{
				Boolean includeCurrentRegion = true;

				if ( regionsList[regionIndex].totalDataPoints <= _minimumRegionDataPoints )
				{
					includeCurrentRegion = false;
				}

				if (includeCurrentRegion)
				{
					// construct point pipeline for this region
					pointsListsArray[regionIndex] = new List<Tuple<int,int>>();

					RegionHullGenerator hullBuilder = null;
					PathFilter			pathFilter = null;

					if (_minimumDistanceBetweenPoints > 0 || _maximumAngleDeltaRadians > 0)
					{
						// if path options specified, put a path filter in the pipe
						// hullBuilder -> pathFilter -> pointsListArray[regionIndex]
						pathFilter = new PathFilter(new PathFilter.Parameters
						{
							minimumDistanceBetweenPoints = _minimumDistanceBetweenPoints,
							maximumAngleRadians = _maximumAngleDeltaRadians
						},
						(point) => { pointsListsArray[regionIndex].Add(point); });

						hullBuilder = new RegionHullGenerator((point) => { pathFilter.HandlePoint(point); });
					}
					else
					{
						// if no path options specified, generate straight to list
						hullBuilder = new RegionHullGenerator((point) => { pointsListsArray[regionIndex].Add(point); });
					}
					
					hullBuilder.generate(regionsList[regionIndex]);

					totalPoints += pointsListsArray[regionIndex].Count;
				}
				else
				{
					pointsListsArray[ regionIndex ] = new List<Tuple<int,int>>();
				}
			});

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
			svgBuilder.SetBackgroundColor( _backgroundColor_Hex );

			foreach (var currentPoints in pointsListsArray)
			{
				svgBuilder.addPath(currentPoints, _contourLineColor_Hex );
			}

            svgBuilder.CreateFile(_outputFilename + ".svg");

			timer.Stop();
			addTiming("svg creation", timer.ElapsedMilliseconds);
        }

		// --------------------------------------------------------------------------------
		// TESTING
		// TODO: better place to put tests, use proper harness, yada yada
		// complicated by fact we need _data already loaded, and it's hard to mock, need to fix that...
		// --------------------------------------------------------------------------------
		private void RunTests()
		{
			Test_SingleRowRegionIsIgnoredByHullBuilder();
			Test_SingleColumnRegionIsIgnoredByHullBuilder();
		}

		// --------------------------------------------------------------------------------
		private void Test_SingleRowRegionIsIgnoredByHullBuilder()
		{
			Console.WriteLine("Testing single row region is ignored");

			if (false == _data.isQuantized)
			{
				_data.Quantize(_contourHeights);
			}

			_data.SetRegionToHeight(0,0, 100,0, 100.0f);

			var regionalizerSetup = new FLTDataRegionalizer.RegionalizerSetupData();
			regionalizerSetup.topoData = _data;

			var testRectIndices = new int[4];
			testRectIndices[RectLeftIndex] = 0;
			testRectIndices[RectTopIndex] = 0;
			testRectIndices[RectRightIndex] = 100;
			testRectIndices[RectBottomIndex] = 0;
			regionalizerSetup.RectIndices = testRectIndices;			

			var regionalizer = new FLTDataRegionalizer(regionalizerSetup);

			regionalizer.GenerateRegions();

			var regionList = regionalizer.RegionList();

			// expect 1 region here (not really the test, I know, should make a test for this)
			if (regionList.Count != 1)
			{
				Console.WriteLine("FAILED (unexpected number of regions ("+ regionList.Count +") in test data)");
				return;
			}
			else
			{
				/*
				var hullBuilder = new RegionHullBuilder();

				var pointsList = hullBuilder.generate(regionList[0]);

				if (pointsList.Count == 0)
				{
					Console.WriteLine("PASSED");
				}
				else
				{
					Console.WriteLine("FAILED (unexpected number of regions ("+ pointsList.Count +") in test data");
				}
				*/
			}
		}

		// --------------------------------------------------------------------------------
		private void Test_SingleColumnRegionIsIgnoredByHullBuilder()
		{
			Console.WriteLine("Testing single column region is ignored");

			if (false == _data.isQuantized)
			{
				_data.Quantize(_contourHeights);
			}

			_data.SetRegionToHeight(0,0, 100,0, 100.0f);

			var regionalizerSetup = new FLTDataRegionalizer.RegionalizerSetupData();
			regionalizerSetup.topoData = _data;

			var testRectIndices = new int[4];
			testRectIndices[RectLeftIndex] = 0;
			testRectIndices[RectTopIndex] = 0;
			testRectIndices[RectRightIndex] = 0;
			testRectIndices[RectBottomIndex] = 100;
			regionalizerSetup.RectIndices = testRectIndices;			

			var regionalizer = new FLTDataRegionalizer(regionalizerSetup);

			regionalizer.GenerateRegions();

			var regionList = regionalizer.RegionList();

			// expect 1 region here (not really the test, I know, should make a test for this)
			// there's actually a bug in this
			if (regionList.Count != 1)
			{
				Console.WriteLine("FAILED (unexpected number of regions ("+ regionList.Count +") in test data)");
				return;
			}
			else
			{
				/*
				var hullBuilder = new RegionHullBuilder();

				var pointsList = hullBuilder.generate(regionList[0]);

				if (pointsList.Count == 0)
				{
					Console.WriteLine("PASSED");
				}
				else
				{
					Console.WriteLine("FAILED (unexpected number of regions ("+ pointsList.Count +") in test data");
				}
				*/
			}
		}

		// TESTING CODE DUMPING GROUNDS
			// SVG TEST STUFF
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
			/*
			_rectIndices[RectLeftIndex] = 0;
			_rectIndices[RectTopIndex] = 0;
			_rectIndices[RectRightIndex] = 1000;
			_rectIndices[RectBottomIndex] = 1000;
			*/
	} // end NormalTopoMapGenerator

}

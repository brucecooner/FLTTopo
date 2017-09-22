using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SVGBuilder;

// TODO:
// -translate generated points to origin
// -move point generation to central location
// -optimize paths


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
		// TODO: discover gnarly edge cases that may break this.. e.g. single point regions, single ROW regions, etc.
		// More edge cases : single point spans
		// TODO: shouldn't live in a particular generator's class file, make into general purpose method somewhere (probably base class?)
		private List<Tuple<int,int>> getListOfRegionEdgeCoords(FLTDataRegionalizer.Region region)
		{
			// start at, like, left edge top left most span
			FLTDataRegionalizer.Span startSpan = region.minRowMinColSpan;

			var currentSpan = startSpan;

			var pointsList = new List<Tuple<int,int>>();

			// maybe a tuple, 
			Tuple<int,int> currentPoint = null;

			// It seems important to note that all travel is counter-clockwise around the region.
			// 1 = positive (down), -1 = negative (up)
			var currentRowTravelDirection = 1;

			Boolean done = false;

			while ( false == done)
			{
				//Console.WriteLine("loop current span id : " + currentSpan.Id + " traveling " + (currentRowTravelDirection > 0 ? "down" : "up"));

				if (currentRowTravelDirection > 0)
				{
					// traveling down
					// get point from LEFT end of current span because travel is CCW around the region, so if traveling down
					// must be going down left side
					currentPoint = new Tuple<int,int>(currentSpan.start, currentSpan.row);
					pointsList.Add(currentPoint);

					// If there are no more spans to follow downward, start going up.
					if (false == currentSpan.hasSpansBelow)
					{
						// leave current span same, next loop iteration will begin 
						// going up at right edge of current span
						currentRowTravelDirection = -1;
					}
					else
					{
						// Have a span below this one...

						if (false == currentSpan.hasLeftSibling)
						{
							// If current span has no left sibling, step down to the left most 
							// span below current and continue downward.
							currentSpan = currentSpan.spansBelow.First();
						}
						else
						{
							// Have a left sibling...
							// Check to see if current span and its left sibling share the span below.
							if (		(currentSpan.spanLeft.hasSpansBelow)
									&&	(currentSpan.spansBelow.First().Id == currentSpan.spanLeft.spansBelow.Last().Id) )
							{
								// if the leftmost span below current overlaps its left sibling, step
								// to point below left end of current...
								currentPoint = new Tuple<int,int>(currentSpan.start, currentSpan.row + 1);
								pointsList.Add(currentPoint);
								// step over to point on span below, that is beneath left sibling's end...
								currentPoint = new Tuple<int,int>(currentSpan.spanLeft.end, currentSpan.row + 1);
								pointsList.Add(currentPoint);
								// set currentSpan to left sibling and begin upward traveling from there
								currentSpan = currentSpan.spanLeft;
								currentRowTravelDirection = -1;
							}
							else
							{
								// current span and left don't share a sibling (means shared span is above)
								currentSpan = currentSpan.spansBelow.First();
							}
						}
					} // end has spans below

				} // end if travel direction down
				else
				{
					// traveling up
					// get point from RIGHT end of current span because travel is CCW around the region, so if traveling up
					// must be going up right side
					currentPoint = new Tuple<int,int>(currentSpan.end, currentSpan.row);
					pointsList.Add(currentPoint);

					// attempt 3 :)
					// If there are no more spans to follow upward, start going down again.
					if (false == currentSpan.hasSpansAbove)
					{
						currentRowTravelDirection = 1;
					}
					else
					{
						// Have a span above this one, but we need to see if it overlaps a sibling 
						// to current span's right.
						if (false == currentSpan.hasRightSibling)
						{
							// If current span has no right sibling, just move up to the right most 
							// span above current and continue upward.
							currentSpan = currentSpan.spansAbove.Last();
						}
						else
						{
							// Have a right sibling, but we have to see if current and its right
							// sibling share the span above. (todo: more descriptive comment here)
							if (		(currentSpan.spanRight.hasSpansAbove)
									&&	(currentSpan.spansAbove.Last().Id == currentSpan.spanRight.spansAbove.First().Id) )
							{
								// if the rightmost span above current overlaps its right sibling, step
								// to point above right end of current...
								currentPoint = new Tuple<int,int>(currentSpan.end, currentSpan.row - 1);
								pointsList.Add(currentPoint);
								// step over to point on span above, that is over right sibling's start...
								currentPoint = new Tuple<int,int>(currentSpan.spanRight.start, currentSpan.row - 1);
								pointsList.Add(currentPoint);
								// set currentSpan to right sibling and begin downward traveling from there
								currentSpan = currentSpan.spanRight;
								currentRowTravelDirection = 1;
							}
							else
							{
								// the rightmost span above current does not overlap its right 
								// sibling, set current to span above and continue normally
								currentSpan = currentSpan.spansAbove.Last();
							}
						}
					} // end attempt 3

				} // end else traveling up

				// detect if returned to the start span and halt
				// If my deductive abilities are correct, that last step will always be upward, because we SHOULD
				// always have started at the minimum (i.e. highest on screen) row span.
				// NOTE: after the halt case make sure we don't skip adding the final point from the startSpan. This should
				// always be the right end of the startSpan, because we should always be starting at the left end of the span
				if (currentSpan == startSpan)
				{
					currentPoint = new Tuple<int,int>(currentSpan.end, currentSpan.row);
					pointsList.Add(currentPoint);
					done = true;
				}
			}	// end while !done

			return pointsList;
		}

        // --------------------------------------------------------------
        private void generateSVG()
        {
            // -- quantize --
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);

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

			
			var regionalizerSetup = new FLTDataRegionalizer.RegionalizerSetupData();
			regionalizerSetup.topoData = _data;
			regionalizerSetup.RectIndices = this._rectIndices;


			var regionalizer = new FLTDataRegionalizer(regionalizerSetup);

			regionalizer.GenerateRegions();

			Console.WriteLine("Making points lists");
			var pointsLists = new List<List<Tuple<int,int>>>();

			foreach (var currentRegion in regionalizer.RegionList())
			{
				Console.WriteLine("Processing region : " + currentRegion.Id);

				var currentPoints = getListOfRegionEdgeCoords(currentRegion);

				Console.WriteLine("generated points : " + currentPoints.Count);

				pointsLists.Add(currentPoints);
			}


			Console.WriteLine("Building svg");
            // alllllright, I guess we need to, like, make an svg now
            var svgBuilder = new SVGBuilder.Builder();

			// TODO: use width/height of generated rect
			//svgBuilder.SetWidthAndHeight( rectRight - rectLeft + 1, rectBottom - rectTop + 1);
			svgBuilder.SetWidthAndHeight( 10812, 10812);

			foreach (var currentPoints in pointsLists)
			{
				//Console.WriteLine("Adding path...");
				svgBuilder.addPath(currentPoints);
			}

            svgBuilder.TestMakingAFile("testFirstPath.svg");
        }
	}

}

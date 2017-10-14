using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FLTTopoContour
{
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
                currentPixel = _backgroundColor_RGBA;

                Int32 evenOdd = Convert.ToInt32(highestValue / _contourHeights % 2);

                if (evenOdd <= 0)
                {
                    currentPixel = _color1_RGBA;
                }
                else
                {
                    currentPixel = _color2_RGBA;
                }
            }
            else
            {
                currentPixel = _backgroundColor_RGBA;
            }

            return currentPixel;
        }

		// -----------------------------------------------------------------------------------------------
		private void generateBMP()
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

        // ============================================================
		private class RegionData
		{
			public float height = 0;

			public List<Tuple<int,int>> points = new List<Tuple<int,int>>();
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

			var regionDataArray = new RegionData[regionsList.Count];

			Parallel.For(0, regionsList.Count, regionIndex =>
			{
				Boolean includeCurrentRegion = true;

				// get height of current region
				regionDataArray[regionIndex] = new RegionData { height = regionsList[regionIndex].regionValue };

				if ( regionsList[regionIndex].totalDataPoints <= _minimumRegionDataPoints )
				{
					includeCurrentRegion = false;
				}

				if (includeCurrentRegion)
				{
					// construct point pipeline for this region
					regionDataArray[regionIndex].points = new List<Tuple<int,int>>();

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
						(point) => { regionDataArray[regionIndex].points.Add(point); });

						hullBuilder = new RegionHullGenerator((point) => { pathFilter.HandlePoint(point); });
					}
					else
					{
						// if no path options specified, generate straight to list
						hullBuilder = new RegionHullGenerator((point) => { regionDataArray[regionIndex].points.Add(point); });
					}
					
					hullBuilder.generate(regionsList[regionIndex]);

					totalPoints += regionDataArray[regionIndex].points.Count;
				}
				/*
				else
				{
					regionDataArray[ regionIndex ].points = new List<Tuple<int,int>>();
				}
				*/
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

			foreach (var currentData in regionDataArray)
			{
                Int32 evenOdd = Convert.ToInt32(currentData.height / _contourHeights % 2);

				String currentPathColor = "#000000";

                if (evenOdd <= 0)
                {
                    currentPathColor = _color1_Hex;
                }
                else
                {
                    currentPathColor = _color2_Hex;
                }


				svgBuilder.addPath(currentData.points, currentPathColor );
			}

            svgBuilder.CreateFile(_outputFilename + ".svg");

			timer.Stop();
			addTiming("svg creation", timer.ElapsedMilliseconds);
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
    }
}

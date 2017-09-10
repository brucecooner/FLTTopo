using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// TODO: 
// -constrainged region detection
// -region merge

namespace FLTTopoContour
{
    // class concerned with finding regions of contiguous values in a quantized set of flt data

	// NOTE: I've added 'rectIndices' which will control the area that gets regionalized. They specify a sub-rect
	// of the flt data. Note, however, that the coordinates kept in the spans will remain in flt topo space, and NOT
	// in rect relative space. Some translation may be necessary for some operations.

    public class FLTDataRegionalizer
    {
        // utility types
        // ---------------------------------------
        // describes a horizontal run of contiguous points of the same value
        public class Span
        {
            public int start = 0;   // start and end columns
            public int end = 0;

            public int row = 0;

            // these should be sorted on column start values, for reasons
            public List<Span> spansAbove = new List<Span>();    // spans where row < this.row
            public List<Span> spansBelow = new List<Span>();    // spans where row > this.row
        }

        // ================================================================
        // describes a 2-d area of points of the same value which all touch
        // as a series of spans
        public class Region
        {
            // ---- region ID ----
            static uint _nextRegionID = 1;
            private static uint getNextRegionID()
            {
                uint returnID = _nextRegionID;
                _nextRegionID += 1;
                return returnID;
            }
            // ID value that indicates no region assigned
            public const uint NO_REGION_ID = 0;

            public uint Id = 0;

            // ---- 
            public float regionValue = 0;

            public List<Span> spanList = new List<Span>();

            // span with the minimum column AMONG the spans with the minimum row
            public Span minRowMinColSpan = null;

            public int minCol = int.MaxValue;
            public int maxCol = int.MinValue;

            public int minRow = int.MaxValue;
            public int maxRow = int.MinValue;

            // TODO: figure out casts to make unsigned type
            public long totalDataPoints = 0; // total data points in region

            // ----------------------------------
            public Region()
            {
                Id = getNextRegionID();
            }

            // ----------------------------------
            public void AddSpan(Span spanToAdd)
            {
                spanList.Add(spanToAdd);

                minCol = Math.Min(minCol, spanToAdd.start);
                maxCol = Math.Max(maxCol, spanToAdd.end);

                minRow = Math.Min(minRow, spanToAdd.row);
                maxRow = Math.Max(maxRow, spanToAdd.row);

                totalDataPoints += spanToAdd.end - spanToAdd.start + 1;

                if (null == minRowMinColSpan)
                {
                    minRowMinColSpan = spanToAdd;
                }
                else
                {
                    if (spanToAdd.row < minRowMinColSpan.row)
                    {
                        minRowMinColSpan = spanToAdd;
                    }
                    else
                    {
                        if (spanToAdd.row == minRowMinColSpan.row)
                        {
                            if (spanToAdd.start < minRowMinColSpan.start)
                            {
                                minRowMinColSpan = spanToAdd;
                            }
                        }
                    }
                }
            }
        }

        // =======================================
        // converts row, col to region ID, NO_REGION_ID indicates no region exists there yet
        private uint[,] _locationToRegionIDMap = null;

        // returns whether or not specified location already contained in a region (any region)
        private Boolean isLocationInARegion(int row, int col)
        {
            return _locationToRegionIDMap[row, col] != Region.NO_REGION_ID;
        }

        // ---- regions ----
        // maps region ID to Region
        public Dictionary<uint, Region> regionIDToRegionMap = null;

        // as list of regions
        public List<Region> RegionList()
        {
            return regionIDToRegionMap.Values.ToList<Region>();
        }

        // ---- data ----
        // the topo data being regionalized, is assumed to be quantized
        private FLTDataLib.FLTTopoData _topoData;

        // ---- rect extents ----
		// indices into flt data controlling area that gets regionalized
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

		protected int rectNumRows { get { return rectBottom - rectTop + 1; } }
		protected int rectNumCols { get { return rectRight - rectLeft + 1; } }

		// ---- setup data ----
		public class RegionalizerSetupData
		{
			public FLTDataLib.FLTTopoData	topoData;
            public int[]					RectIndices;
		}

        // ---------------------------------------
        // constructor
        public FLTDataRegionalizer(RegionalizerSetupData setupData)
        {
            if (false == setupData.topoData.IsInitialized())
            {
                throw new System.InvalidOperationException("FLTDataRegionalizer: topoData not initialized");
            }
            if (false == setupData.topoData.isQuantized)
            {
                throw new System.InvalidOperationException("FLTDataRegionalizer: topoData not quantized");
            }

            _topoData = setupData.topoData;

			_rectIndices = setupData.RectIndices;

			// TODO: validate rect!
		}

        // -------------------------------
        // returns contiguous horizontal locations with same topo height as start location
        // receives: region that will contain span
        // NOTE that this is the only location where values in locationToRegionIDMap will be set!!!
        private Span getSpan(int row, int col, Region containingRegion)
        {
            var newSpan = new Span();

            newSpan.start = col;
            newSpan.end = col;
            newSpan.row = row;

            float matchValue = _topoData.ValueAt(row, col);

            // look left
            Boolean done = false;
            int currentColumn = col;

            while (false == done)
            {
                // shouldn't need to check if already owned here, should never meet a same valued location 
                // belonging to a different region
                if (		(currentColumn >= rectLeft)
                        &&	(matchValue == _topoData.ValueAt(row, currentColumn))
                    )
                {
                    newSpan.start = currentColumn;
                    _locationToRegionIDMap[row, currentColumn] = containingRegion.Id;
                }
                else
                {
                    // found a halt condition (edge, different value)
                    done = true;
                }

                currentColumn -= 1;
            }

            // look right
            currentColumn = col + 1;
            done = false;

            while (false == done)
            {
                if (		(currentColumn <= rectRight ) // _topoData.NumCols)
                        &&	(matchValue == _topoData.ValueAt(row, currentColumn))
                    )
                {
                    newSpan.end = currentColumn;
                    _locationToRegionIDMap[row, currentColumn] = containingRegion.Id;
                }
                else
                {
                    done = true;
                }

                currentColumn += 1;
            }

            return newSpan;
        }

        // ----------------------------------------------------------------
        // gets spans on specified row that horizontally overlap specified sourceSpan
        // new spans are contained by containingRegion
        // -these neighbor spans may extend PAST ends of sourceSpan
        // -may return an empty list
        private List<Span> getNeighborSpans(Span sourceSpan, int row, Region containingRegion)
        {
            var newSpans = new List<Span>();

            float matchValue = _topoData.ValueAt(sourceSpan.row, sourceSpan.start);

            // find first location above sourceSpan that matches and has not been checked
            int currentCol = sourceSpan.start;

            // keep finding spans until we're past the end of the source span
            while (		(currentCol <= sourceSpan.end)
					&&	(currentCol <= rectRight ) //_topoData.NumCols)
                    )
            {
                if (		(false == isLocationInARegion(row, currentCol))
                        &&	(matchValue == _topoData.ValueAt(row, currentCol))
                    )
                {
                    var newSpan = getSpan(row, currentCol, containingRegion);

                    newSpans.Add(newSpan);

                    if (row < sourceSpan.row)
                    {
                        sourceSpan.spansAbove.Add(newSpan);

                        newSpan.spansBelow.Add(newSpan);
                    }
                    else
                    {
                        sourceSpan.spansBelow.Add(newSpan);

                        newSpan.spansAbove.Add(newSpan);
                    }

                    currentCol = newSpan.end + 1;
                }
                else
                {
                    currentCol += 1;
                }
            }

            return newSpans;
        }

        // ----------------------------------------------------------------
        // finds region of points that touch specified row,col in topoData that are of same value
        private Region getRegion(int row, int col)
        {
            var newRegion = new Region();

            newRegion.regionValue = _topoData.ValueAt(row, col);

            var seedSpan = getSpan(row, col, newRegion);

            var spansToCheck = new List<Span>();

            spansToCheck.Add(seedSpan);

            while (spansToCheck.Count > 0)
            {
                Span checkSpan = spansToCheck[0];
                spansToCheck.RemoveAt(0);

                // get spans above
                if (checkSpan.row > rectTop ) // 0)
                {
                    var aboveSpans = getNeighborSpans(checkSpan, checkSpan.row - 1, newRegion);

                    spansToCheck.AddRange(aboveSpans);
                }

                // get spans below
                if (checkSpan.row < rectBottom ) // _topoData.NumRows - 1)
                {
                    var belowSpans = getNeighborSpans(checkSpan, checkSpan.row + 1, newRegion);

                    spansToCheck.AddRange(belowSpans);
                }

                // done checking, add span to region
                newRegion.AddSpan(checkSpan);
            }

            return newRegion;
        }

        // -------------------------------
        // clears data structures, prepares to find regions again in
        // same _topoData
        // note : does not re-allocate already allocated structures
        public void Reset()
        {
            if (null == regionIDToRegionMap)
            {
                regionIDToRegionMap = new Dictionary<uint, Region>();
            }
            else
            {
                regionIDToRegionMap.Clear();
            }

            if (null == _locationToRegionIDMap)
            {
				// note that a full size map (size of topo data) is used here, regardless of rect size/placement
				// this allows usage of coordinates in the original (map) space without 
				// having to translate to/from rect space
                _locationToRegionIDMap = new uint[_topoData.NumRows, _topoData.NumCols];
            }

			//for (int row = 0; row < _topoData.NumRows; row += 1)
			// note : initializing entire location 2 region map
			Parallel.For(0, _topoData.NumRows, currentRow =>
            {
                for (int col = 0; col < _topoData.NumCols; col += 1)
                {
                    _locationToRegionIDMap[currentRow, col] = Region.NO_REGION_ID;
                }
            });
        }

        // -------------------------------
        public void GenerateRegions()
        {
            Reset();

            // ---- generate ----
            //for (int row = 0; row < _topoData.NumRows; row += 1)
			for (int row = rectTop; row <= rectBottom; row += 1)
            {
                //for (int col = 0; col < _topoData.NumCols; col += 1)
				for (int col = rectLeft; col <= rectRight; col += 1)
                {
                    if (false == isLocationInARegion(row, col))
                    {
                        var newRegion = getRegion(row, col);

                        regionIDToRegionMap[newRegion.Id] = newRegion;
                    }
                }
            }

#if false
            // ---- integrity checks? ----
            // every point in data should be in a region
            for (int row = 0; row < _topoData.NumRows; row += 1)
            {
                for (int col = 0; col < _topoData.NumCols; col += 1)
                {
                    if (false == isLocationInARegion(row, col))
                    {
                        throw new System.Exception("Regionalization did not cover point at " + row + "," + col);
                    }
                }
            }
#endif
        }   // end GenerateRegions()

        // ---------------------------------------------------------------------------
        public Region getRegionAt(int row, int col)
        {
            Region returnRegion = null;

            if (false == isLocationInARegion(row,col))
            {
                throw new System.InvalidOperationException("location at " + row + "," + col + " does not map to a region");
            }

            returnRegion = regionIDToRegionMap[_locationToRegionIDMap[row, col]];

            return returnRegion;
        }

        // ---------------------------------------------------------------------------
        // gets a region "adjacent" to the specified region that meets a minimum total data points size criteria
        // NOTE: a region may be contained within another region that does not meet the criteria. In this case, the 
        // containing region itself will be used as the start region for selection.
        public Region getNeighboringRegionOfMinimumSize(Region sourceRegion, long minimumTotalDataPoints)
        {
            Region returnRegion = null;

            // generally, quantized regions will touch two other regions (the next height above and below), but in an area
            // with vertical bluffs this rule may not apply

            var checkedRegions = new List<Region>();

            // NOTE: halts at first region that meets criteria
            // ALSO NOTE: only checks left/right of spans. May want to incorporate neighbor row checks, but most of
            // those would just find the current region (since spans are horizontal)
            foreach ( var currentSpan in sourceRegion.spanList)
            {
                if (currentSpan.start > rectLeft ) //0)
                {
                    var checkRegion = getRegionAt(currentSpan.row, currentSpan.start - 1);
                    checkedRegions.Add(checkRegion);

                    if (checkRegion.totalDataPoints >= minimumTotalDataPoints)
                    {
                        returnRegion = checkRegion;
                        break;
                    }
                }

                if (currentSpan.end < rectRight ) //_topoData.NumCols - 1)
                {
                    var checkRegion = getRegionAt(currentSpan.row, currentSpan.end + 1);
                    checkedRegions.Add(checkRegion);

                    if (checkRegion.totalDataPoints >= minimumTotalDataPoints)
                    {
                        returnRegion = checkRegion;
                        break;
                    }
                }
            }

            // it's possible the source region is contained entirely within a region that does not meet the minimum size criteria
            // in this case go through the regions we checked and find a region adjacent to one of them that meets the criteria
            if (null == returnRegion)
            {
                // it is highly UNlikely that a region has no neighbors (that would mean it's the only region, i.e. the
                // topo data was completely flat), but we'll check anyway
                if (0 == checkedRegions.Count)
                {
                    throw new System.Exception("No regions were checked in initial tests.");
                }

                returnRegion = getNeighboringRegionOfMinimumSize(checkedRegions[0], minimumTotalDataPoints);

                if (null == returnRegion)
                {
                    throw new System.Exception("Was unable to find region matching minimum total data points criteria");
                }
            }

            return returnRegion;
        }

        // ---------------------------------------------------------------------------
        public List<String> getStats()
        {
            var returnStrings = new List<String>();

            returnStrings.Add("num regions: " + regionIDToRegionMap.Count);

            long minimumTotalDataPoints = int.MaxValue;
            long maximumTotalDataPoints = int.MinValue;
            long totalRegionalDataPoints = 0;

            long totalNumberOfSpans = 0;

            foreach (var currentRegion in RegionList())
            {
                minimumTotalDataPoints = Math.Min(currentRegion.totalDataPoints, minimumTotalDataPoints);
                maximumTotalDataPoints = Math.Max(currentRegion.totalDataPoints, maximumTotalDataPoints);

                totalRegionalDataPoints += currentRegion.totalDataPoints;

                totalNumberOfSpans += currentRegion.spanList.Count;
            }

            returnStrings.Add("minimum data points: " + minimumTotalDataPoints);
            returnStrings.Add("maximum data points: " + maximumTotalDataPoints);
            returnStrings.Add("total data points (all regions): " + totalRegionalDataPoints);
            returnStrings.Add("total spans (all regions): " + totalNumberOfSpans);

            return returnStrings;
        }
    }
}

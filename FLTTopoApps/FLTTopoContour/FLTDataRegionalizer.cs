using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FLTTopoContour
{
    // class concerned with finding regions of contiguous values in a quantized set of flt data
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

        // ---------------------------------------
        // describes a 2-d area of points of the same value which all touch
        // as a series of spans
        public class Region
        {
            public float regionValue = 0;

            public List<Span> spanList = new List<Span>();

            // span with the minimum column AMONG the spans with the minimum row
            Span minRowMinColSpan = null;

            int minCol = int.MaxValue;
            int maxCol = int.MinValue;

            int minRow = int.MaxValue;
            int maxRow = int.MinValue;

            public int totalDataPoints = 0; // total data points in region

            public void AddSpan( Span spanToAdd )
            {
                spanList.Add( spanToAdd );

                minCol = Math.Min(minCol, spanToAdd.start);
                maxCol = Math.Max(maxCol, spanToAdd.end);

                minRow = Math.Min(minRow, spanToAdd.row);
                maxRow = Math.Max(maxRow, spanToAdd.row);

                totalDataPoints += spanToAdd.end - spanToAdd.start + 1;

                if ( null == minRowMinColSpan )
                {
                    minRowMinColSpan = spanToAdd;
                }
                else
                {
                    if ( spanToAdd.row < minRowMinColSpan.row )
                    {
                        minRowMinColSpan = spanToAdd;
                    }
                    else
                    {
                        if ( spanToAdd.row == minRowMinColSpan.row)
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
        // array to track row,col locations in topo data that have already 
        // been checked for inclusion with a region
        private Boolean[,] _checkedLocations = null;

        // all regions found in topo data
        List<Region> _regionList;

        // bit of a hot mess here, what's going on?
        public List<Region> RegionList() { return _regionList; }

        // the topo data being regionalized, is assumed to be quantized
        private FLTDataLib.FLTTopoData _topoData;

        // ---------------------------------------
        // constructor
        public FLTDataRegionalizer(FLTDataLib.FLTTopoData inputTopoData)
        {
            if ( false == inputTopoData.IsInitialized() )
            {
                throw new System.InvalidOperationException("topoData not initialized");
            }
            if ( false == inputTopoData.isQuantized )
            {
                throw new System.InvalidOperationException("topoData not quantized");
            }

            _topoData = inputTopoData;
        }

        // -------------------------------
        // returns contiguous horizontal locations with same topo height as start location
        // NOTE that this is the only location where values in _checkedLocations will be set!!!
        private Span getSpan(int row, int col)
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
                // shouldn't need to check _checkedLocations here, should never meet a same valued location 
                // belonging to a different region
                if (        ( currentColumn >= 0 )
                        &&  ( matchValue == _topoData.ValueAt(row, currentColumn) ) 
                    )
                {
                    newSpan.start = currentColumn;
                    _checkedLocations[row, currentColumn] = true;
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
                if (        ( currentColumn < _topoData.NumCols )
                        &&  ( matchValue == _topoData.ValueAt(row, currentColumn) )
                    )
                {
                    newSpan.end = currentColumn;
                    _checkedLocations[row, currentColumn] = true;
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
        // -sub spans may extend past ends of sourceSpan
        // -may return an empty list
        private List<Span> getNeighborSpans( Span sourceSpan, int row )
        {
            var newSpans = new List<Span>();

            float matchValue = _topoData.ValueAt(sourceSpan.row, sourceSpan.start);

            // find first location above sourceSpan that matches and has not been checked
            int currentCol = sourceSpan.start;

            // keep finding spans until we're past the end of the source span
            while (       (currentCol <= sourceSpan.end )
                        &&  (currentCol < _topoData.NumCols )
                    )
            {
                if (        ( false == _checkedLocations[row, currentCol ] )
                        &&  ( matchValue == _topoData.ValueAt(row, currentCol) )
                    )
                {
                    var newSpan = getSpan(row, currentCol);

                    newSpans.Add(newSpan);

                    if ( row < sourceSpan.row )
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

            var seedSpan = getSpan(row, col);

            var spansToCheck = new List<Span>();

            spansToCheck.Add( seedSpan );

            while ( spansToCheck.Count > 0 )
            {
                Span checkSpan = spansToCheck[0];
                spansToCheck.RemoveAt(0);

                // get spans above
                if ( checkSpan.row > 0 )
                {
                    var aboveSpans = getNeighborSpans(checkSpan, checkSpan.row - 1);

                    spansToCheck.AddRange(aboveSpans);
                }
                
                // get spans below
                if ( checkSpan.row < _topoData.NumRows - 1 )
                {
                    var belowSpans = getNeighborSpans(checkSpan, checkSpan.row + 1);

                    spansToCheck.AddRange(belowSpans);
                }

                // done checking, add span to region
                newRegion.AddSpan( checkSpan );
            }

            return newRegion;
        }

        // -------------------------------
        public void GenerateRegions()
        {
            _regionList = new List<Region>();

            _checkedLocations = new Boolean[_topoData.NumRows, _topoData.NumCols];
            for (int row = 0; row < _topoData.NumRows; row += 1)
            {
                for (int col = 0; col < _topoData.NumCols; col += 1)
                {
                    _checkedLocations[row, col] = false;
                }
            }

            for ( int row = 0; row < _topoData.NumRows; row += 1 )
            {
                for ( int col = 0; col < _topoData.NumCols; col += 1 )
                {
                    if ( false == _checkedLocations[row, col] )
                    {
                        var newRegion = getRegion(row, col);

                        _regionList.Add(newRegion);
                    }
                }
            }

            _checkedLocations = null;
        }
    }
}

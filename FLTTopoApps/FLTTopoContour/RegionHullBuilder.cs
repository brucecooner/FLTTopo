using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FLTTopoContour
{
	// "Crawls" around the outside of a specified Region (see:FLTDataRegionalizer), composing a list of points as it travels.
	// This should represent the minimally enclosing 'hull' around the region (ignoring any of its interior voids).
	// TODO:
	// - find a less awkward name?
	// - discover/handle edge cases e.g. single point regions, single ROW regions, single point spans
	// - consider optimization of points as they are added (no duplicate points, minimum delta, etc.)
	// - region bounds that are added twice, where two regions meet (happens with those at edge of rect)
	class RegionHullBuilder
	{
		// ---- points ----
		private List<Tuple<int,int>> _pointsList = new List<Tuple<int,int>>();

		// --------------------------------------------------------------
		private void addPoint(Tuple<int,int> pointToAdd)
		{
			// todo: point rejection/optimization logic
			_pointsList.Add(pointToAdd);
		}

		// --------------------------------------------------------------
		public void Reset()
		{
			_pointsList.Clear();
		}

		// --------------------------------------------------------------
		private Boolean isRegionIgnored(FLTDataRegionalizer.Region region)
		{
			Boolean ignored = false;

			if (region.spanList.Count == 1)
			{
				// single span (and thus row) region
				ignored = true;
			}
			else if (region.minCol == region.maxCol)
			{
				// single column region
				ignored = true;
			}

			return ignored;
		}

		// --------------------------------------------------------------
		public List<Tuple<int,int>> getListOfRegionEdgeCoords(FLTDataRegionalizer.Region region)
		{
			// Notes: 
			// - As regions are taken from data points arranged like pixels on a screen, direction labels are relative to a coordinate
			//   system with origin at top left corner, with x increasing to the 'right' and y increasing 'down'.
			// - Algorithm starts at the top left (i.e. northwestern) most point in the region, and crawls around the region in a
			//   counter-clockwise manner.

			// test rejection cases
			if (isRegionIgnored(region))
			{
				return new List<Tuple<int,int>>();
			}

			// start at left edge of left most span on minimum row (which region helpfully already knows)
			var startSpan = region.minRowMinColSpan;

			var currentSpan = startSpan;

			//var pointsList = new List<Tuple<int,int>>();

			Tuple<int,int> currentPoint = null;

			// travel direction correlates to movement on the rows (1 ==> downward), though no math is done
			// using this, it is strictly a logical indicator
			const int UP = -1;
			const int DOWN = 1;
			var currentRowTravelDirection = DOWN;

			Boolean done = false;

			while ( false == done)
			{
				//Console.WriteLine("loop current span id : " + currentSpan.Id + " traveling " + (currentRowTravelDirection > 0 ? "down" : "up"));

				if (DOWN == currentRowTravelDirection)
				{
					// traveling down
					// get point from LEFT end of current span because travel is CCW around the region, so if traveling down
					// must be going down left side
					currentPoint = new Tuple<int,int>(currentSpan.start, currentSpan.row);
					addPoint(currentPoint);

					// If there are no more spans to follow downward, start going up.
					if (false == currentSpan.hasSpansBelow)
					{
						// leave current span same, next loop iteration will begin 
						// going up at right edge of current span
						currentRowTravelDirection = UP;
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
							// Check to see if current span and its left sibling share the span below (and not above).
							// Picture this as a little 'bowl' with the bottom formed by the span below, and the sides
							// formed by this span and its left sibling.
							if (		(currentSpan.spanLeft.hasSpansBelow)
									&&	(currentSpan.spansBelow.First().Id == currentSpan.spanLeft.spansBelow.Last().Id) )
							{
								// traverse the bottom of the 'bowl'
								// step to point below left end of current span...
								currentPoint = new Tuple<int,int>(currentSpan.start, currentSpan.row + 1);
								addPoint(currentPoint);
								// step over to point on span below that is beneath left sibling's end...
								currentPoint = new Tuple<int,int>(currentSpan.spanLeft.end, currentSpan.row + 1);
								addPoint(currentPoint);
								// set currentSpan to left sibling, and begin upward traveling from there
								currentSpan = currentSpan.spanLeft;
								currentRowTravelDirection = UP;
							}
							else
							{
								// current span and left don't share a sibling (means shared span is above)
								// continue downward normally
								currentSpan = currentSpan.spansBelow.First();
							}
						} // end else has left sibling
					} // end has spans below

				} // end if traveling down
				else
				{
					// traveling up
					// get point from RIGHT end of current span because travel is CCW around the region, so if traveling up
					// must be going up right side
					currentPoint = new Tuple<int,int>(currentSpan.end, currentSpan.row);
					addPoint(currentPoint);

					// If there are no more spans to follow upward, start going down again (from same span).
					if (false == currentSpan.hasSpansAbove)
					{
						currentRowTravelDirection = DOWN;
					}
					else
					{
						// Have a span above this one, but we need to see if it overlaps a sibling 
						// to current span's right.
						if (false == currentSpan.hasRightSibling)
						{
							// If current span has no right sibling, move up to the right most 
							// span above current and continue upward.
							currentSpan = currentSpan.spansAbove.Last();
						}
						else
						{
							// Have a right sibling.
							// See if right sibling and current span share a span above (and not below).
							// Picture this situation as an upside down bowl, or lowercase 'n', with the sides
							// formed by currentSpan and its right sibling, and the 'top' formed by 
							// the span above.
							if (		(currentSpan.spanRight.hasSpansAbove)
									&&	(currentSpan.spansAbove.Last().Id == currentSpan.spanRight.spansAbove.First().Id) )
							{
								// step to point above right end of current...
								currentPoint = new Tuple<int,int>(currentSpan.end, currentSpan.row - 1);
								addPoint(currentPoint);
								// step over to point on span above that is over right sibling's start...
								currentPoint = new Tuple<int,int>(currentSpan.spanRight.start, currentSpan.row - 1);
								addPoint(currentPoint);
								// set currentSpan to right sibling and begin downward travel from there
								currentSpan = currentSpan.spanRight;
								currentRowTravelDirection = DOWN;
							}
							else
							{
								// no shared span with sibling (can ignore sibling)
								// set current to span above and continue normally
								currentSpan = currentSpan.spansAbove.Last();
							}
						}
					} // end else has above span 

				} // end else traveling up

				// detect if returned to the start span and halt
				// If my deductive abilities are correct, that last step will always be upward, because we SHOULD
				// always have started at the minimum (i.e. highest on screen) row span.
				// NOTE: after the halt case make sure we don't skip adding the final point from the startSpan. This should
				// always be the right end of the startSpan, because we should always be starting at the left end of the span
				if (currentSpan == startSpan)
				{
					currentPoint = new Tuple<int,int>(currentSpan.end, currentSpan.row);
					addPoint(currentPoint);
					done = true;
				}
			}	// end while !done

			return _pointsList;
		}
	}
}

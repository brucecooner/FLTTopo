using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FLTTopoContour
{
	// responsible for taking a path of 2d integer (currently) points and filtering by some settings
	// NOTE: assumes path points are absolute (world) coordinates, and NOT relative to previously added point(s)
	// TODO:
	// - use algorithm that increases point density based on path complexity to preserve curvy parts?
	public class PathFilter
	{
		// ======================================
		public class Parameters
		{
			// if >0, points are not added to path until one is found that is greater
			// than this distance from the last added point
			public double minimumDistanceBetweenPoints = 0;

			// if > 0, when the segment between the two previous candidate points turns by more 
			// than this angle then the points are added (regardless of distance constraint)
			public double maximumAngleRadians = 0; // TODO: better name
		}

		private Parameters _parameters = new Parameters();

		private int _numberOfAddedPoints = 0;

        // function delegate to specify callback for points
        public delegate void addPointDelegate( Tuple<int,int> point );

		// called when a point is added to path
		private addPointDelegate _addPointHandler = null;

		// ---- candidate points ----
		// 0->most recent candidate point
		// 1->previous candidate point
		// point 2->previous previous candidate point
		// yeah, could be a Queue but I want random access
		private Tuple<int,int>[] _lastThreeCandidatePoints = new Tuple<int,int>[3] { null, null, null };

		// prevent redundant adding of points
		private Boolean[] _candidatesAdded = new Boolean[3] { false, false, false };

		private Tuple<int,int> currentCandidatePoint			{ get { return _lastThreeCandidatePoints[0]; } }
		private Tuple<int,int> previousCandidatePoint			{ get { return _lastThreeCandidatePoints[1]; } }
		private Tuple<int,int> previousPreviousCandidatePoint	{ get { return _lastThreeCandidatePoints[2]; } } // better name?

		// ---- candidate segments ----
		// same as points 0->most, recent 1->previous
		// note : as name implies, keep these normalized
		private Tuple<double,double>[] _lastTwoCandidateSegmentsNormalized = new Tuple<double,double>[2] { null, null };

		// these properties assume segments are already calculated
		private Tuple<double,double>	currentCandidateSegment		{ get { return _lastTwoCandidateSegmentsNormalized[0]; } }
		private Tuple<double,double>	previousCandidateSegment	{ get { return _lastTwoCandidateSegmentsNormalized[1]; } }

		// total number of candidates submitted
		private int _totalCandidatePoints = 0;

		// last point to be added to path
		private Tuple<int,int> _lastAddedPoint = null;

		private Tuple<double,double> _lastAddedSegmentNormalized = null;

		// running total of path distance since the last point was added
		private double _distanceSinceLastPoint = 0;

		// -------------------------------------------------------
		private void addCandidatePoint( Tuple<int,int> newCandidatePoint )
		{
			// shift array toward upper index, new point at index 0
			_lastThreeCandidatePoints[2] = _lastThreeCandidatePoints[1];
			_lastThreeCandidatePoints[1] = _lastThreeCandidatePoints[0];
			_lastThreeCandidatePoints[0] = newCandidatePoint;

			_candidatesAdded[2] = _candidatesAdded[1];
			_candidatesAdded[1] = _candidatesAdded[0];
			_candidatesAdded[0] = false;

			_totalCandidatePoints += 1;

			if ( _totalCandidatePoints >= 2 )
			{
				var currentCandidateSegmentInt = Vector.Int.Delta(previousCandidatePoint,currentCandidatePoint);

				// must do here before normalization
				_distanceSinceLastPoint += Vector.Int.Length(currentCandidateSegmentInt);

				_lastTwoCandidateSegmentsNormalized[1] = _lastTwoCandidateSegmentsNormalized[0];
				_lastTwoCandidateSegmentsNormalized[0] = Vector.Int.Normalize(currentCandidateSegmentInt);
			}
		}

		// -------------------------------------------------------
		// constructor
		public PathFilter(	Parameters			setupParameters, 
							addPointDelegate	addPointHandler )
		{
			_addPointHandler = addPointHandler;
			_parameters = setupParameters;
		}

		// -----------------------------------------------------------
		private Boolean minimumDistanceConstraintIsOn
		{ get { return _parameters.minimumDistanceBetweenPoints > 0 ? true : false; } }

		// note that this returns false if minimum distance constraint is off, since every point will 
		// be added anyway
		private Boolean maximumAngleConstraintIsOn
		{ get { return _parameters.maximumAngleRadians > 0 && (true == minimumDistanceConstraintIsOn) ? true : false; } }

		// -----------------------------------------------------------
		// returns true if specified point deviates enough from something something to add
		private Boolean segmentsTriggerAngleConstraint( Tuple<double, double> segment1Normalized, Tuple<double,double> segment2Normalized )
		{
			bool triggersConstraint = false;

			var dot = Vector.Double.Dot( segment1Normalized, segment2Normalized );

			var angle = Math.Acos( dot );

			if ( angle >= _parameters.maximumAngleRadians )
			{
				triggersConstraint = true;
			}

			return triggersConstraint;
		}

		// -----------------------------------------------------------
		private void addPoint( Tuple<int,int> newPoint )
		{
			_addPointHandler(newPoint);

			_numberOfAddedPoints += 1;

			// keep the delta between the last two added points around
			if (null != _lastAddedPoint)
			{
				var lastAddedSegment = Vector.Int.Delta( _lastAddedPoint, newPoint );
				_lastAddedSegmentNormalized = Vector.Int.Normalize( lastAddedSegment );
			}

			_lastAddedPoint = newPoint;

			_distanceSinceLastPoint = 0;
		}

		// -----------------------------------------------------------
		public void HandlePoint(Tuple<int,int> candidatePoint)
		{
			addCandidatePoint( candidatePoint );

			// should never encounter zero here after add, but will check anyway
			if ( 0 == _totalCandidatePoints )
			{
				throw new System.Exception("PathFilter.HandlePoint() : zero candidate points");
			}
			else if ( 1 == _totalCandidatePoints )
			{
				// first point encountered, add by default
				addPoint( currentCandidatePoint );

				_candidatesAdded[0] = true;
			}
			else
			{
				if (		false == maximumAngleConstraintIsOn
						&&	false == minimumDistanceConstraintIsOn)
				{
					addPoint(currentCandidatePoint);
				}
				else
				{
					Boolean triggeredAngleConstraint = false;

					// note: testing angular constraint first, if it triggers we want to ensure
					// current and previous candidate points are added, whereas the distance constraint
					// only triggers addition of current point, which would be technically incorrect if
					// the angular constraint did trigger
					// also note : cannot test with <2 candidate points and <2 added points (to generate segments to compare)
					if (maximumAngleConstraintIsOn && (_totalCandidatePoints >= 3))
					{
						if (segmentsTriggerAngleConstraint(previousCandidateSegment,currentCandidateSegment))
						{
							// add the current AND previous points to capture this turn in the path
							if (false == _candidatesAdded[1])
							{
								addPoint(previousCandidatePoint);
								_candidatesAdded[1] = true;
							}

							addPoint(currentCandidatePoint);

							_candidatesAdded[0] = true;

							triggeredAngleConstraint = true;
						}
					}

					// note : do not test if angle constraint was triggered
					if (minimumDistanceConstraintIsOn && (false == triggeredAngleConstraint))
					{
						if (_distanceSinceLastPoint >= _parameters.minimumDistanceBetweenPoints)
						{
							addPoint(currentCandidatePoint);
							_candidatesAdded[0] = true;
						}
					}
				}
			}

		} // end HandlePoint()

	} // end class PathFilter

	// ==============================================================================
	// I know, I know, proper testing units and all that
	class PathFilterTest
	{
		// -----------------------------------------------------------------
		public void TestDistanceCriteria()
		{
			var testPointList = new List<Tuple<int,int>>();

			var testFilter = new PathFilter(new PathFilter.Parameters { minimumDistanceBetweenPoints = 5, maximumAngleRadians = 0 },
											(point) => { testPointList.Add(point); } );

			testFilter.HandlePoint( new Tuple<int,int>( 0, 0 ));    // should be added
			testFilter.HandlePoint( new Tuple<int,int>( 0, 10 ));	// should be added
			testFilter.HandlePoint( new Tuple<int,int>( 10, 10 ));	// should be added
			testFilter.HandlePoint( new Tuple<int,int>( 11,11 ));	// should NOT be added

			// path should have added three points
			if (testPointList.Count == 3)
			{
				Console.WriteLine("PathFilter distance test passed.");
			}
			else
			{
				Console.WriteLine("PathFilter distance test FAILED.");
			}
		}

		// -----------------------------------------------------------------
		public void TestDistancePlusAngleCriteria()
		{
			var testPointList = new List<Tuple<int,int>>();

			// disable distance
			var testFilter = new PathFilter(new PathFilter.Parameters { minimumDistanceBetweenPoints = 50,
																		maximumAngleRadians = 80 * Constants.Trig.DegreesToRadians }, 
											(point) => { testPointList.Add(point); } );

			testFilter.HandlePoint( new Tuple<int,int>( 0, 0 ));    // should be added
			testFilter.HandlePoint( new Tuple<int,int>( 10, 0 ));   // should not be added here (distance)
			testFilter.HandlePoint( new Tuple<int,int>(10,10));		// should be added with previous point (90 degree turn, down)
			testFilter.HandlePoint( new Tuple<int,int>(20,20));		// should not be added (45 degree turn), no distance triggered
			testFilter.HandlePoint( new Tuple<int,int>(20,80));		// should be added (distance triggered)

			if (testPointList.Count == 4)
			{
				Console.WriteLine("PathFilter angle test passed.");
			}
			else
			{
				Console.WriteLine("PathFilter angle test FAILED.");
			}
		}

		// -----------------------------------------------------------------
	}
}





using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FLTTopoContour
{
	// responsible for taking a path of 2d integer (currently) points and filtering by some settings
	public class PathFilter
	{
		// ======================================
		public class Parameters
		{
			public double minimumDistanceBetweenPoints = 0;

			// TODO: implement angle constraint
			// public float maximumAngleQuestionMark = 0;
		}

		private Parameters _parameters = new Parameters();

		private int _numberOfAddedPoints = 0;

        // function delegate to specify callback for points
        public delegate void addPointDelegate( Tuple<int,int> point );

		private addPointDelegate _addPointHandler = null;

		// -------------------------------------------------------
		// constructor
		public PathFilter(	Parameters			setupParameters, 
							addPointDelegate	addPointHandler )
		{
			_addPointHandler = addPointHandler;
			_parameters = setupParameters;
		}

		// -----
		private Tuple<int,int> _lastAddedPoint = null;

		// difference between last two added points
		private Tuple<int,int>	_lastAddedDelta = null;

		// -----------------------------------------------------------
		private Boolean pointMeetsMinimumDistanceCriteria( Tuple<int,int> testPoint )
		{
			Boolean meetsDistanceCriteria = true;

			// avoid ops if minimum distance was not specified
			if (_parameters.minimumDistanceBetweenPoints > 0)
			{
				var distanceFromLastAddedPoint = Vector.Int.Length( _lastAddedPoint, testPoint );

				meetsDistanceCriteria = (distanceFromLastAddedPoint > _parameters.minimumDistanceBetweenPoints) ? true : false;
			}

			return meetsDistanceCriteria;
		}

		// -----------------------------------------------------------
		// returns true if specified point deviates enough from something something to add
		private Boolean pointMeetsMaximumAngleCriteria(Tuple<int,int> testPoint)
		{
			// TODO: write this
			return false;
		}

		// -----------------------------------------------------------
		public void handlePoint(Tuple<int,int> candidatePoint)
		{
			Boolean shouldAddPoint = false;

			if (0 == _numberOfAddedPoints )
			{
				shouldAddPoint = true;
			}
			else if (1 == _numberOfAddedPoints)
			{
				shouldAddPoint = pointMeetsMinimumDistanceCriteria( candidatePoint );
			}
			else
			{
				// deviates enough to force add?
				shouldAddPoint = pointMeetsMaximumAngleCriteria(candidatePoint);

				// may not deviate, but still are checking minimum distance criteria
				if (pointMeetsMinimumDistanceCriteria(candidatePoint))
				{
					shouldAddPoint = true;
				}
			}

			// -------------------------
			if (shouldAddPoint)
			{
				// state maintenance
				_numberOfAddedPoints += 1;

				// get delta before overwriting last point...
				var previousLastPoint = _lastAddedPoint;

				_lastAddedPoint = candidatePoint;

				// do some calcs?
				if (previousLastPoint != null)
				{
					_lastAddedDelta = Vector.Int.Delta(previousLastPoint, _lastAddedPoint);
				}

				_addPointHandler( candidatePoint );
			}
		}
	}
}

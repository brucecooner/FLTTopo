using System;
using System.Collections.Generic;

namespace Timing
{
	// =========================================================================================
	class Timer 
	{
		long number_of_timings = 0;

		float total_milliseconds = 0;

        private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();

		public float AverageMilliseconds {  get { return total_milliseconds / number_of_timings; } }
		public float AverageSeconds {  get { return AverageMilliseconds / 1000.0f; } }

		public float ElapsedMilliseconds {  get { return total_milliseconds; } }
		public float ElapsedSeconds { get { return ElapsedMilliseconds / 1000.0f; } }

		// -----------------------
		public void Start()
		{
			_stopwatch.Reset();
			_stopwatch.Start();
		}

		// ----------------------
		public void Stop()
		{
			_stopwatch.Stop();

			number_of_timings += 1;
			total_milliseconds += _stopwatch.ElapsedMilliseconds;
		}

		// ---------------------
		public Timer(Boolean startImmediately)
		{
			if (startImmediately)
			{
				Start();
			}
		}

	}	// end class Timer
}

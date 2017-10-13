namespace Constants
{
    public class Distance
    {
        public const double MilesPerArcMinute = 1.15077945;  // ye olde nautical mile, (the equatorial distance / 360) / 60 
        public const double MilesPerDegree = MilesPerArcMinute * 60.0f;    // about 69 miles (at the equator)
        public const double MetersPerMile = 1609.34;
        public const double MetersPerDegree = MetersPerMile * MilesPerDegree;
        public const double DegreesPerMeter = 1.0 / MetersPerDegree;
    }

	public class Trig
	{
		public const double DegreesToRadians = 0.0174532925;
	}
}

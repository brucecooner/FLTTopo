using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// TODO: bezier/quadratic options for paths
namespace SVGBuilder
{
	// ==========================================================================================
    class Builder
    {
		private const String _defaultPathColor = "#000000";

		private class Path
		{
			public String colorHexTriplet = "#000000";

			public List<Tuple<int,int>>	points = new List<Tuple<int,int>>();
		}

		private long _width = 0;
		private long _height = 0;

		//private List<List<Tuple<int,int>>> _paths = new List<List<Tuple<int,int>>>();
		private List<Path>	_paths = new List<Path>();

		private long _XTranslate = 0;
		private long _YTranslate = 0;


		// --------------------------------------------
		// constructor 
        public Builder()
        {

        }

		// -----------------------------------------------------------
		public void SetWidthAndHeight( long newWidth, long newHeight )
		{
			_width = newWidth;
			_height = newHeight;
		}

		// -----------------------------------------------------------
		public void SetTranslate( long xTranslate, long yTranslate )
		{
			_XTranslate = xTranslate;
			_YTranslate = yTranslate;
		}

		// -----------------------------------------------------------
		public void addPath(List<Tuple<int,int>> pathPoints, String pathColor = _defaultPathColor)
		{
			// ignore empty paths
			if (pathPoints.Count > 0)
			{
				_paths.Add( new Path { points = pathPoints, colorHexTriplet = pathColor } );
			}
		}

		// ------------------------------------------------------------
		// TODO : 
		// -validate parameters
		// -investigate where to put newlines
		private void generatePath(System.IO.StreamWriter file, Path sourcePath )
		{
			//  <path d="M150 0 L75 200 L225 200 Z" />
			file.Write( "<path ");

			file.Write("stroke=\"" + sourcePath.colorHexTriplet + "\" stroke-width=\"1\" fill=\"none\"");
				
			file.Write(" d=\"" );

			Boolean first = true;

			foreach (var currentPoint in sourcePath.points)
			{
				String prefix = first ? "M" : "L";
				first = false;

				file.Write(prefix + (currentPoint.Item1 + _XTranslate) + " " + (currentPoint.Item2 + _YTranslate) + " ");
			}

			// close path
			file.WriteLine("Z\"/>");
		}

        // -------------------------------------------------
        public void CreateFile(String filename)
        {
			using (System.IO.StreamWriter file =
				new System.IO.StreamWriter(filename))
			{
				file.WriteLine("<svg version = \"1.1\"");
				file.WriteLine("baseProfile = \"full\"");
				file.WriteLine("width = \"{0}\" height = \"{1}\"",_width,_height);

				file.WriteLine("xmlns = \"http://www.w3.org/2000/svg\">");

				//file.WriteLine("<rect width = \"100%\" height = \"100%\" fill = \"red\" />");
				//file.WriteLine("<circle cx=\"150\" cy=\"100\" r=\"80\" fill=\"green\" />");
				//file.WriteLine("<text x=\"150\" y=\"125\" font-size=\"60\" text-anchor=\"middle\" fill=\"white\" > SVG </ text >");

				foreach (var currentPath in _paths)
				{
					generatePath( file, currentPath );
				}

				file.WriteLine("</svg>");


			}
        }
    }

}

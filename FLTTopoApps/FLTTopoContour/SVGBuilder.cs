using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVGBuilder
{
    class Builder
    {
		private long _width = 0;
		private long _height = 0;

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

        // -------------------------------------------------
        public void TestMakingAFile()
        {
            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(@"TestSVGFile.svg"))
            {
				file.WriteLine("<svg version = \"1.1\"");
				file.WriteLine("baseProfile = \"full\"");
				file.WriteLine("width = \"{0}\" height = \"{1}\"", _width, _height);

				file.WriteLine("xmlns = \"http://www.w3.org/2000/svg\">");

				//file.WriteLine("<rect width = \"100%\" height = \"100%\" fill = \"red\" />");
                //file.WriteLine("<circle cx=\"150\" cy=\"100\" r=\"80\" fill=\"green\" />");
                //file.WriteLine("<text x=\"150\" y=\"125\" font-size=\"60\" text-anchor=\"middle\" fill=\"white\" > SVG </ text >");

                file.WriteLine("</svg>");


            }
        }
    }

}

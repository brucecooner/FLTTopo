using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SVGBuilder;

namespace FLTTopoContour
{
    class NormalTopoMapGeneratorSVG : TopoMapGenerator
    {
        public override String GetName() { return "svg"; }

        // ---- constructor ----
        public NormalTopoMapGeneratorSVG(GeneratorSetupData setupData) : base(setupData)
        {}

        // --------------------------------------------------------------
        public override void Generate()
        {
            // -- quantize --
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            _data.Quantize(_contourHeights);

            stopwatch.Stop();
            addTiming("quantization", stopwatch.ElapsedMilliseconds);

			var regionalizerSetup = new FLTDataRegionalizer.RegionalizerSetupData();
			regionalizerSetup.topoData = _data;
			regionalizerSetup.RectIndices = this._rectIndices;

			var regionalizer = new FLTDataRegionalizer(regionalizerSetup);

			regionalizer.GenerateRegions();

            // alllllright, I guess we need to, like, make an svg now
            var builder = new SVGBuilder.Builder();

            builder.TestMakingAFile();
        }
    }
}

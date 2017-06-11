# FLTTopo
FLTTopo is a utility (and supporting libraries) written in C# to convert USGS flt/hdr height data into arbitrarily contoured topographical maps (in bitmap format).

Feed FLTTopoContour a pair of hdr (header) and flt (data) files from the USGS National Map Viewer download service (http://viewer.nationalmap.gov/viewer/) and it can output a rough, but properly contoured topographical map with contours on integral heights that you can specify (the default is 200).  

It was developed in Windows and currently only outputs .bmp images. Nothing really fancy is happening here, it's just quantizing the height data and doing edge detection on the result. That said, its output can be useful for certain projects.

Check out the Gallery folder for some example maps generated with this application.

It's not just limited to traditional contour maps though, a few other modes are available:
* gradient: points go from one color at the lowest point to a different color at the highest point (essentially a quantized version of the map, sometimes useful in its own right)
* alternating: alternating contours appear in different colors, this can be useful if you are stacking layers by printing two maps, cutting each on a different set of contours and using the alternating colors as references for the alternating colored layer immediately above
* horizontal slice: each contour line appears in its own separate file, useful with maps with steep slopes and/or vertical cliffs, where contours overlap
* vertical slice: produces a series of vertical 'slices' of an area, slices may be north/south or east/west oriented, and frequency/separation of the slices can be controlled
* report: just report some information on flt/hdr data (extents, minimum and maximum heights)

Notes:
* You can set all colors used in all the modes.
* You can constrain the output to only a sub-rect of the input data, specified via lat/long coordinates or as indices within the source data.
* You can control the dimensions of output images. The program will compute a dimension if it is unspecified, maintaining the aspect ratio of the input rect.
* Run with the ? option to see options and program notes.
* Look for the "HowToGetFLTData.txt" file for instructions on getting source data to feed to the app.

Caveat Emptor:
* I could not find a way to control the extents of the data the USGS website viewer allows you to select and download. The minimum they will ship you is a 1 degree by 1 degree area, which comes out to a lot of real estate (about 69 miles on a side at the equator, decreasing with latitude). So you'll probably have a map covering a much larger area than you were looking for. I've added options to the program to allow you to specify an inset area of the map to process and output (by dataset indices or lat/long coordinates).

* The feature you want to map may lie across a 1 degree border, and therefore not be constructable from a single input file. You should be able to produce two maps from adjacent grids' data and join them in a graphics editing program.

* You'll need 1/3 arcsecond source data to get nice smooth output. This means there'll be quite a lot of input data. The 1x1 degree grids run to several hundred megabytes.

* It's only been built and run under Windows so far. No funky libraries are in use, Xamarin will probably happily build it, but I haven't looked into compatibility with other systems or using an image library to support multiple formats. The FLTDataLib does the data reading work, and should be a decent core to build whatever port/utility you want on top of.

* It currently only supports raster output, and the contour lines aren't smoothed. Sorry, this isn't intended to be a study in graphics techniques (and the output is usable for my purposes in a pixelated form). You should, of course, be able to generate a gradient type map and apply edge detection in a graphics editing program to produce different styles of line if you like. I've fed its output to a couple of SVG converters with decent results.

* I haven't done much work toward making this truly distributable software,  you'll have to mind the placement of the flt data library dll so the program works.

* I know constructable isn't a word, but I found it was cromulent to my needs.

Licensed under GPLv3.

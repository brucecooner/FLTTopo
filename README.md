# FLTTopo
FLTTopo is a utility (and supporting libraries) written in C# to convert USGS flt/hdr height data into arbitrarily contoured topographical maps (in bitmap format)

Feed FLTTopoContour a pair of hdr (header) and flt (data) files from the USGS National Map Viewer download service (http://viewer.nationalmap.gov/viewer/) and it can output a rough, but properly contoured topographical map with contours on integral heights that you can specify (the default is 200).  

It was developed in Windows and currently only outputs .bmp images.

It's not just limited to traditional contour maps though, a few other modes are available:
* gradient: points go from one color at the lowest point to a different color at the highest point
* alternating: alternating contours appear in different colors (useful for physical projects where you want to stack contour layers cut from a thick medium).
* horizontal slice: each contour line appears in its own separate file
* vertical slice: produces maps that are vertical rather than horizontal
* report: just get some information on the flt/hdr data (extents, minimum and maximum heights). 

Notes:
* You can set all colors used in all the modes.
* You can constrain the output to only a sub-rect of the input data.
* You can control the dimensions of output images (the program will compute a dimension if it is unspecified, maintaining the aspect ratio of the input area)
* Run with the ? option to see options and program notes.
* Look for the "HowToGetFLTData.txt" file for instructions on getting source data to feed to the app.

Caveat Emptor:
* I could not find a way to control the extents of the data the viewer allows you to select and download. The minimum they will ship you is a 1 degree by 1 degree area, which comes out to a lot of real estate (about 69 miles on a side at the equator, decreasing with latitude). So you'll probably have a map covering a much larger area than you were looking for. I've added options to the program to allow you to specify an inset area of the map to process and output (by dataset indices or lat/long coordinates). 

* The feature you want to use may lie across a 1 degree border, and therefore not be constructable from a single input file. You could produce two maps and join them in a graphics editing program however.

* You'll need 1/3 arcsecond source data to get nice smooth output. This means there'll be quite a lot of input data. The 1x1 degree grids run to several hundred megabytes.

* It's only been built and run under Windows so far. Xamarin might build it okay, but I haven't looked into compatibility with other systems or using an image library to support multiple formats. The FLTDataLib does the data reading work, and should be a decent core to build whatever port/utility you want on top of.

* I don't pretty up the output images in any way, the lines won't be as smooth an an official map, but any number of image processing tools out there could possibly do what you want.

* I haven't done much work toward making this truly distributable software,  you'll have to work with placement of the flt data library dll so the program works. 

Licensed under GPLv3. 

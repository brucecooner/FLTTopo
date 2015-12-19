# FLTTopo
FLTTopo is a utility (and supporting libraries) written in C# to convert USGS flt/hdr height data into arbitrarily contoured topographical maps (in bitmap format)

Feed FLTTopoContour a pair of hdr (header) and flt (data) files from the USGS National Map Viewer download service (http://viewer.nationalmap.gov/viewer/) and it will output a rough, but properly contoured topographical map with contours on integral heights that you can specify (the default is 200). You can optionally output a grayscale image where the lowest and highest points discovered in the data become the black and white values in the image. 

It was developed in Windows and currently only outputs .bmp images.

There are some options. You can output a continuous color gradient map, an 'alternating' map in which alternating contours appear in different colors (useful for physical projects where you want to stack contour layers cut from a thick medium), or you can just get a report on the map data (extents, min/max). You can set the colors used in all the modes.

Look for the "HowToGetFLTData.txt" file for instructions on getting source data to feed to the app.

Gotchas:
-I could not find a way to control the extents of the data the viewer allows you to select and download. The minimum they will ship you is a 1 degree by 1 degree area, which comes out to a lot of real estate (about 69 miles on a side at the equator, decreasing with latitude). So you'll probably have a map covering a much larger area than you were looking for. I've added options to the program to allow you to specify an inset area of the map to process and output (by dataset indices or lat/long coordinates). 

-Unfortunately you'll only get nice smooth output with 1/3 arcsecond input data. This means there'll be quite a lot of it. The source data for input comes in at a few hundred megabytes. I've optimized a bit with a few parallel for's, maxing out all cores on my Corei7 and getting the runtime down from ~2.5 minutes to about 10-30 seconds, but further optimizations are probably possible.

-It's only been built and run under Windows so far. Xamarin might build it okay, but I haven't looked into compatibility with other systems or using an image library to support multiple formats. The FLTDataLib does the data reading work, and should be a decent core to build whatever port/utility you want on top of.

-I don't pretty up the output images in any way, the lines won't be as smooth an an official map, but any number of image processing tools out there can do what you want.

-I haven't done much work toward making this truly distributable software,  you'll have to work with placement of the flt data library dll so the program works. 

Licensed under GPLv3. 

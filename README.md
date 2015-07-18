# FLTTopo
FLTTopo is a utility (and supporting libraries) written in C# to convert USGS flt/hdr height data into arbitrarily contoured topographical maps (in bitmap format)

The main utility here is FLTTopoContour. There are others in the solution but it's been a while since I fiddled with this and I can't remember what all of them do. I was mostly focused on generating topo maps so once I got to that goal I quit tinkering.

Anyway, feed FLTTopoContour a pair of hdr (header) and flt (data) files from the USGS National Map Viewer download service (http://viewer.nationalmap.gov/viewer/) and it will output a contoured topographical map with contours on integral heights that you can specify (the default is 200). You can optionally output a grayscale image where the lowest and highest points discovered in the data become the black and white values in the image. It was developed in Windows and currently only outputs .bmp images.

Gotchas:
-I could not find a way to control the extents of the data the viewer allows you to select and download. If I remember right (been a while since I last messed with it), it forced me to specify a minimum 3x3 quadrangle, which comes out to a lot of real estate. You'll probably have a map covering a much larger area than you were looking for, and will just have to use an image editor to select the area you're interested in out of the final image. I think one of the utilities I was working on was to extract a portion of the height data from the original set, though this isn't terribly useful without being able to see and inspect the data in the first place.

-Unfortunately you'll only get nice smooth output with 1/3 arcsecond input data. This means there'll be quite a lot of it. The source data for input comes in at about half a gigabyte. I've optimized a bit with a few parallel for's, maxing out all cores on my Corei7 and getting the runtime down from ~2.5 minutes to about 10-30 seconds, but further optimizations are probably possible.

-It's only been built and run under Windows so far. Xamarin might build it okay, but I haven't looked into compatibility with other systems or using an image library to support multiple formats. The FLTDataLib does the data reading work, and should be a decent core to build whatever port/utility you want on top of.

-I don't pretty up the output images in any way, post processing is left to any number of image specific tools.

Licensed under GPLv3. 

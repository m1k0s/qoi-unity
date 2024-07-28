# qoi-unity

Unity plugin for encoding & decoding QOI format images (https://qoiformat.org/qoi-specification.pdf).

Implementation is split into:
* a zero allocation pure C# ([QOI](Assets/QOI/QOI.cs)) [`ReadOnlySpan`](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1?view=net-8.0)/[`Span`](https://learn.microsoft.com/en-us/dotnet/api/system.span-1?view=net-8.0) based "namespace" static class; very much based on the reference C implementation (https://github.com/phoboslab/qoi) with two minor changes:
    1. decoding has been separated into `DecodeHeader` and `Decode` (so that we get a chance to create an appropriately sized `Span` to decode into)
    2. `flipVertically` has been added to both encoding & decoding since gfx texture resources are typically flipped vertically compared to raster image data
* a Unity layer [QOIUnity](Assets/QOI/QOIUnity.cs) that can decode into or encode from a Unity `Texture2D`
    * uses `NativeArrayUnsafeUtility` to work directly with the native pixel data (at the appropriate mip-level if required)
    * only `RGBA32` and `RGB24` texture formats are supported
    * encoding does require the source `Texture2D` to be readable

The sample Unity project includes a test UGUI scene that loads, displays & saves the test images referenced in https://qoiformat.org/

Prerquisites:
* Tested with Unity 2021.3.16f1

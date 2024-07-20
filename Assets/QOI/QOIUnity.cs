using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using System;

public static class QOIUnity
{
    public static void Read(ref QOI.Header header, ReadOnlySpan<byte> buffer, Texture2D texture, int mipLevel)
    {
        var data = texture.GetPixelData<byte>(mipLevel);

        unsafe
        {
            var pixels = new Span<byte>(NativeArrayUnsafeUtility.GetUnsafePtr(data), data.Length);
            QOI.Decode(buffer, header.width, header.height, header.channels, pixels);
        }
    }
    
    public static Texture2D Read(ReadOnlySpan<byte> buffer)
    {
        if (QOI.DecodeHeader(buffer, out var header))
        {
            var format = header.channels == 4 ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            var linear = header.colorspace == QOI.Colorspace.Linear;

            var texture = new Texture2D((int)header.width, (int)header.height, format, false, linear);
            Read(ref header, buffer, texture, 0);

            return texture;
        }
        
        return null;
    }
    
    public static bool WriteHeader(Texture2D texture, out QOI.Header header)
    {
        int channels = 0;
        switch (texture.format)
        {
            case TextureFormat.RGBA32:
                channels = 4;
                break;
            case TextureFormat.RGB24:
                channels = 3;
                break;
            default:
                header = new ();
                return false;
        }

        header.width = (uint)texture.width;
        header.height = (uint)texture.height;
        header.channels = (byte)channels;
        // header.colorspace = texture.isDataSRGB ? QOI.Colorspace.SRGB : QOI.Colorspace.Linear;
        header.colorspace = QOI.Colorspace.SRGB;

        return true;
    }

    public static uint Write(Texture2D texture, ref QOI.Header header, Span<byte> buffer)
    {
        var data = texture.GetPixelData<byte>(0);
        uint length;

        unsafe
        {
            var pixels = new ReadOnlySpan<byte>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data), data.Length);
            length = QOI.Encode(pixels, ref header, buffer);
        }

        return length;
    }
}

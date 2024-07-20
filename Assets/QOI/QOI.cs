using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

public static class QOI
{
    public enum Colorspace : byte
    {
        SRGB = 0,
        Linear = 1
    }

    public struct Header
    {
        public const uint SIZE = 14;
        public uint width;
        public uint height;
        public byte channels;
        public Colorspace colorspace;

        public uint MaxSize => (uint)(width * height * (channels + 1) + SIZE + PADDING_SIZE);
    }

    private const byte OP_INDEX = 0x00; //< 00xxxxxx
    private const byte OP_DIFF  = 0x40; //< 01xxxxxx
    private const byte OP_LUMA  = 0x80; //< 10xxxxxx
    private const byte OP_RUN   = 0xc0; //< 11xxxxxx
    private const byte OP_RGB   = 0xfe; //< 11111110
    private const byte OP_RGBA  = 0xff; //< 11111111
    private const byte MASK_2   = 0xc0; //< 11000000

    [StructLayout(LayoutKind.Explicit)] 
    private struct RGBA
    {
        public struct _RGBA
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;
        }

        [FieldOffset(0)] public _RGBA rgba;
        [FieldOffset(0)] public uint v;
        
        public uint ColorHash => (uint)rgba.r * 3 + (uint)rgba.g * 5 + (uint)rgba.b * 7 + (uint)rgba.a * 11;
    }

    private const uint PADDING_SIZE = 8; //< byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }

    private const uint MAGIC = (((uint)'q') << 24) | (((uint)'o') << 16) | (((uint)'i') <<  8) | ((uint)'f');
    private const uint PIXELS_MAX = 400000000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void WriteUInt32(byte** p, uint v)
    {
        *(*p)++ = (byte)((0xff000000 & v) >> 24);
        *(*p)++ = (byte)((0x00ff0000 & v) >> 16);
        *(*p)++ = (byte)((0x0000ff00 & v) >> 8);
        *(*p)++ = (byte)(0x000000ff & v);
    }

    public static uint Encode(ReadOnlySpan<byte> pixelData, ref Header header, Span<byte> data, bool flipVertically = false)
    {
        int channels = header.channels;
        if (header.width == 0 || header.height == 0 || channels < 3 || channels > 4 || header.height >= PIXELS_MAX / header.width)
        {
            return 0;
        }
        
        int px_len = (int)(header.width * header.height * channels);
        if (pixelData.Length < px_len)
        {
            return 0;
        }

        Span<RGBA> index = stackalloc RGBA[64];

        RGBA px_prev;
        px_prev.v = 0; //< Silence unassigned error
        px_prev.rgba.r = 0;
        px_prev.rgba.g = 0;
        px_prev.rgba.b = 0;
        px_prev.rgba.a = 255;
        RGBA px = px_prev;

        unsafe
        {
            fixed(byte* pixels = pixelData)
            {
                fixed(byte* bytes = data)
                {
                    byte* src = pixels;
                    byte* dst = bytes;

                    WriteUInt32(&dst, MAGIC);
                    WriteUInt32(&dst, header.width);
                    WriteUInt32(&dst, header.height);
                    *dst++ = header.channels;
                    *dst++ = (byte)header.colorspace;

                    int run = 0;
                    int px_end = px_len - channels;
                    for (int px_pos = 0; px_pos < px_len; px_pos += channels)
                    {
                        px.rgba.r = *src++;
                        px.rgba.g = *src++;
                        px.rgba.b = *src++;

                        if (channels == 4)
                        {
                            px.rgba.a = *src++;
                        }

                        if (px.v == px_prev.v)
                        {
                            ++run;
                            if (run == 62 || px_pos == px_end)
                            {
                                *dst++ = (byte)(OP_RUN | (run - 1));
                                run = 0;
                            }
                        }
                        else
                        {
                            int index_pos;

                            if (run > 0)
                            {
                                *dst++ = (byte)(OP_RUN | (run - 1));
                                run = 0;
                            }

                            index_pos = (int)px.ColorHash % 64;

                            if (index[index_pos].v == px.v)
                            {
                                *dst++ = (byte)(OP_INDEX | index_pos);
                            }
                            else
                            {
                                index[index_pos] = px;

                                if (px.rgba.a == px_prev.rgba.a)
                                {
                                    sbyte vr = (sbyte)(px.rgba.r - px_prev.rgba.r);
                                    sbyte vg = (sbyte)(px.rgba.g - px_prev.rgba.g);
                                    sbyte vb = (sbyte)(px.rgba.b - px_prev.rgba.b);

                                    sbyte vg_r = (sbyte)(vr - vg);
                                    sbyte vg_b = (sbyte)(vb - vg);

                                    if (vr > -3 && vr < 2 && vg > -3 && vg < 2 && vb > -3 && vb < 2)
                                    {
                                        *dst++ = (byte)(OP_DIFF | (vr + 2) << 4 | (vg + 2) << 2 | (vb + 2));
                                    }
                                    else if (vg_r > -9 && vg_r < 8 && vg > -33 && vg < 32 && vg_b > -9 && vg_b < 8)
                                    {
                                        *dst++ = (byte)(OP_LUMA | (vg + 32));
                                        *dst++ = (byte)((vg_r + 8) << 4 | (vg_b + 8));
                                    }
                                    else
                                    {
                                        *dst++ = OP_RGB;
                                        *dst++ = px.rgba.r;
                                        *dst++ = px.rgba.g;
                                        *dst++ = px.rgba.b;
                                    }
                                }
                                else
                                {
                                    *dst++ = OP_RGBA;
                                    *dst++ = px.rgba.r;
                                    *dst++ = px.rgba.g;
                                    *dst++ = px.rgba.b;
                                    *dst++ = px.rgba.a;
                                }
                            }
                        }
                        px_prev = px;
                    }

                    // Add padding (byte[] { 0, 0, 0, 0, 0, 0, 0, 1 })
                    *dst++ = 0;
                    *dst++ = 0;
                    *dst++ = 0;
                    *dst++ = 0;
                    *dst++ = 0;
                    *dst++ = 0;
                    *dst++ = 0;
                    *dst++ = 1;

                    return (uint)(dst - bytes);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint ReadUInt32(byte** p)
    {
        uint a = *(*p)++;
        uint b = *(*p)++;
        uint c = *(*p)++;
        uint d = *(*p)++;
        return a << 24 | b << 16 | c << 8 | d;
    }

    public static bool DecodeHeader(ReadOnlySpan<byte> data, out Header header)
    {
        if (data.Length < Header.SIZE + PADDING_SIZE)
        {
            header = new Header();
            return false;
        }
        
        unsafe
        {
            fixed(byte* bytes = data)
            {
                byte* p = bytes;

                var magic = ReadUInt32(&p);
                var width = ReadUInt32(&p);
                var height = ReadUInt32(&p);
                var channels = *p++;
                var colorspace = *p++;

                if (width == 0 || height == 0 || channels < 3 || channels > 4 || colorspace > 1 || magic != MAGIC || height >= PIXELS_MAX / width)
                {
                    header = new Header();
                    return false;
                }

                header = new Header {
                    width = width,
                    height = height,
                    channels = channels,
                    colorspace = (Colorspace)colorspace
                };
                return true;
            }
        }
    }

    public static bool Decode(ReadOnlySpan<byte> data, uint width, uint height, int channels, Span<byte> pixelData, bool flipVertically = false)
    {
        if (data.Length < Header.SIZE + PADDING_SIZE || width == 0 || height == 0 || height >= PIXELS_MAX / width || channels < 3 || channels > 4)
        {
            return false;
        }
        
        int px_len = (int)(width * height * channels);
        if (pixelData.Length < px_len)
        {
            return false;
        }
        
        int run = 0;
        int px_end = px_len - channels;

        Span<RGBA> index = stackalloc RGBA[64];

        RGBA px;
        px.v = 0; //< Silence unassigned error
        px.rgba.r = 0;
        px.rgba.g = 0;
        px.rgba.b = 0;
        px.rgba.a = 255;

        unsafe
        {
            fixed(byte* pixels = pixelData)
            {
                fixed(byte* bytes = data)
                {
                    byte* src = bytes + Header.SIZE;
                    byte* src_end = bytes + data.Length - PADDING_SIZE;
                    byte* dst = pixels;

                    for (int px_pos = 0; px_pos < px_len; px_pos += channels)
                    {
                        if (run > 0)
                        {
                            run--;
                        }
                        else if (src < src_end)
                        {
                            int b1 = *src++;

                            if (b1 == OP_RGB)
                            {
                                px.rgba.r = *src++;
                                px.rgba.g = *src++;
                                px.rgba.b = *src++;
                            }
                            else if (b1 == OP_RGBA)
                            {
                                px.rgba.r = *src++;
                                px.rgba.g = *src++;
                                px.rgba.b = *src++;
                                px.rgba.a = *src++;
                            }
                            else if ((b1 & MASK_2) == OP_INDEX)
                            {
                                px = index[b1];
                            }
                            else if ((b1 & MASK_2) == OP_DIFF)
                            {
                                px.rgba.r += (byte)(((b1 >> 4) & 0x03) - 2);
                                px.rgba.g += (byte)(((b1 >> 2) & 0x03) - 2);
                                px.rgba.b += (byte)((b1 & 0x03) - 2);
                            }
                            else if ((b1 & MASK_2) == OP_LUMA)
                            {
                                int b2 = *src++;
                                int vg = (b1 & 0x3f) - 32;
                                px.rgba.r += (byte)(vg - 8 + ((b2 >> 4) & 0x0f));
                                px.rgba.g += (byte)vg;
                                px.rgba.b += (byte)(vg - 8 + (b2 & 0x0f));
                            }
                            else if ((b1 & MASK_2) == OP_RUN)
                            {
                                run = (b1 & 0x3f);
                            }

                            index[(int)px.ColorHash % 64] = px;
                        }

                        *dst++ = px.rgba.r;
                        *dst++ = px.rgba.g;
                        *dst++ = px.rgba.b;
                        
                        if (channels == 4)
                        {
                            *dst++ = px.rgba.a;
                        }
                    }
                }
            }
        }

        return true;
    }
}
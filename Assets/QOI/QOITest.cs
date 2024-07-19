using System;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;

public class QOITest : MonoBehaviour
{
    public Texture2D[] textures = new Texture2D[0];

    private static void Write(Texture2D texture, string path)
    {
        if (!texture.isReadable)
        {
            Debug.LogWarning($"{texture.name}: not CPU readable");
            return;
        }

        if(QOIUnity.WriteHeader(texture, out var header))
        {
            byte[] buffer = new byte[header.MaxSize];
            var length = QOIUnity.Write(texture, ref header, new Span<byte>(buffer));
            if (length != 0)
            {
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(buffer, 0, (int)length);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"{texture.name}: encoding failed");
            }
        }
        else
        {
            Debug.LogWarning($"{texture.name}: unsupported texture format {texture.format}");
        }
    }

    void Start()
    {
        foreach (var texture in textures)
        {
            Write(texture, Path.Combine(Application.dataPath, texture.name + ".qoi"));
        }
    }
}

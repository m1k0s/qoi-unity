using System;
using UnityEngine;
using System.IO;

public class QOITest : MonoBehaviour
{
    public Texture2D[] textures = new Texture2D[0];

    void Start()
    {
        foreach (var texture in textures)
        {
            var path = Path.Combine(Application.temporaryCachePath, texture.name + ".qoi");
            try
            {
                Debug.Log($"Writing {texture.name} ({path})");
                QOIUnity.Write(texture, path);
                Debug.Log($"Reading {texture.name} ({path})");
                var readback = QOIUnity.Read(path);
            }
            catch(Exception e)
            {
                Debug.LogError($"Failed to process {texture.name} ({path})\n{e}");
            }
        }
    }
}

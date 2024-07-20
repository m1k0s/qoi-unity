using System;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;

public class Test : MonoBehaviour
{
    public GameObject contentTemplate;
    public VerticalLayoutGroup content;
    public Texture2D[] textures = new Texture2D[0];

    private const float delaySeconds = 1.0f;

    IEnumerator Start()
    {
        var contentTransform = content.GetComponent<RectTransform>();
        int width = 0;
        int height = 0;

        foreach (var texture in textures)
        {
            yield return new WaitForSeconds(delaySeconds);
            
            var path = Path.Combine(Application.temporaryCachePath, texture.name + ".qoi");
            try
            {
                Debug.Log($"Writing {texture.name} ({path})");
                QOIUnity.Write(texture, path);
                Debug.Log($"Reading {texture.name} ({path})");
                var t = QOIUnity.Read(path);
                t.Apply(false, true);

                var sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.zero);
                var tile = Instantiate(contentTemplate);
                tile.name = texture.name;
                var image = tile.GetComponent<Image>();
                image.sprite = sprite;

                var tileRectTransform = tile.GetComponent<RectTransform>();
                tileRectTransform.sizeDelta = new Vector2(t.width, t.height);
                tileRectTransform.SetParent(contentTransform, false);
                
                if (width < t.width)
                {
                    width = t.width;
                }
                height += t.height;
                contentTransform.sizeDelta = new Vector2(width, height);
            }
            catch(Exception e)
            {
                Debug.LogError($"Failed to process {texture.name} ({path})\n{e}");
            }
        }
    }
}

using UnityEditor.AssetImporters;
using System;
using System.IO;

[ScriptedImporter(1, "qoi")]
public class QOIImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var buffer = File.ReadAllBytes(ctx.assetPath);
        var texture = QOIUnity.Read(new ReadOnlySpan<byte>(buffer));
        ctx.AddObjectToAsset("main", texture);
        ctx.SetMainObject(texture);
    }
}

using UnityEditor.AssetImporters;

[ScriptedImporter(1, "qoi")]
public class QOIImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var texture = QOIUnity.Read(ctx.assetPath);
        ctx.AddObjectToAsset("main", texture, texture);
        ctx.SetMainObject(texture);
    }
}

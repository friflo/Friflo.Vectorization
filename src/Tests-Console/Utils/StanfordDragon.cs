
using Setup;

// Copy from (MIT):  https://github.com/EmilSV/Webgpusharp-examples/blob/main/GraphicsTechniques/ShadowMapping/StanfordDragon.cs
static class StanfordDragon
{
    public static async Task<SimpleMeshBinReader.Mesh> LoadMeshAsync()
    {
        var assembly = typeof(StanfordDragon).Assembly;
        await using var stream = assembly.GetManifestResourceStream("Tests-Console.Assets.stanfordDragonData.bin")!;
        return await SimpleMeshBinReader.LoadData(stream);
    }
}
// heights <mapdir> <in.tsv> <out.tsv>: input lines "key asset x z", output "key height areaId" from the server's .nfmap files.
using System.Numerics;
using NexusForever.IO.Map;

var maps = new Dictionary<string, MapFile>();
using var output = new StreamWriter(args[2]);
foreach (string line in File.ReadLines(args[1]))
{
    string[] f = line.Split('\t');
    if (f.Length < 4) continue;
    if (!maps.TryGetValue(f[1], out MapFile map))
    {
        using var stream = File.OpenRead(Path.Combine(args[0], f[1] + ".nfmap"));
        using var reader = new BinaryReader(stream);
        map = new MapFile();
        map.Read(reader);
        maps[f[1]] = map;
    }
    var v = new Vector3(float.Parse(f[2]), 0f, float.Parse(f[3]));
    output.WriteLine($"{f[0]}\t{map.GetTerrainHeight(v)?.ToString() ?? ""}\t{map.GetWorldAreaId(v)?.ToString() ?? ""}");
}

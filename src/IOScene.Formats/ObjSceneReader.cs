using System.Globalization;

namespace IOScene.Formats;

/// <summary>
/// Imports Wavefront OBJ models (.obj) into a scene with one model: each object or group
/// (<c>o</c>/<c>g</c>) becomes one element drawn by one geometry with the vertices its faces use, without changing
/// the up axis. Material (<c>.mtl</c>) files are not read.
/// </summary>
public static class ObjSceneReader
{
    /// <summary>Reads an OBJ file and takes its coordinates as metres with Z up.</summary>
    /// <param name="filePath">Path to the file.</param>
    public static SceneDocument Load(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var fs = File.OpenRead(filePath);
        return Load(fs, Path.GetFileNameWithoutExtension(filePath), Path.GetFileName(filePath), filePath);
    }

    /// <summary>Reads an OBJ stream and closes it when done.</summary>
    /// <param name="stream">Stream positioned at the first OBJ record.</param>
    /// <param name="documentTitle">Name of the resulting scene.</param>
    public static SceneDocument Load(Stream stream, string documentTitle = "Imported OBJ Model")
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Load(stream, documentTitle, "model", null);
    }

    private static SceneDocument Load(Stream stream, string documentTitle, string modelKey, string? path)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

        var vertices = new List<Vector3d>();
        var meshIndices = new List<int>();
        var currentMeshName = "DefaultMesh";
        var doc = new SceneDocument { Title = documentTitle };
        doc.Models.Add(new SceneModel { Id = 1, Key = modelKey, Name = documentTitle, Path = path, SourceFormat = "obj" });
        int meshCounter = 0;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var span = line.AsSpan().Trim();
            if (span.IsEmpty || span[0] == '#') continue;

            if (span.StartsWith("v ") || span.StartsWith("v\t"))
            {
                var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                    double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
                {
                    if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
                        throw new InvalidDataException("An OBJ vertex coordinate is outside the finite range.");
                    vertices.Add(new Vector3d(x, y, z));
                }
            }
            else if (span.StartsWith("f ") || span.StartsWith("f\t"))
            {
                var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    // Triangle or fan triangulation
                    int v0 = ParseFaceVertex(parts[1], vertices.Count);
                    int v1 = ParseFaceVertex(parts[2], vertices.Count);

                    for (int i = 3; i < parts.Length; i++)
                    {
                        int v2 = ParseFaceVertex(parts[i], vertices.Count);
                        if (v0 >= 0 && v1 >= 0 && v2 >= 0)
                        {
                            meshIndices.Add(v0);
                            meshIndices.Add(v1);
                            meshIndices.Add(v2);
                        }
                        v1 = v2;
                    }
                }
            }
            else if (span.StartsWith("o ") || span.StartsWith("g ") || span.StartsWith("o\t") || span.StartsWith("g\t"))
            {
                AddMesh();
                currentMeshName = span[2..].Trim().ToString();
            }
        }

        AddMesh();

        return doc;

        // One mesh and one element per OBJ object or group, so the viewer can list, select and hide it.
        void AddMesh()
        {
            if (meshIndices.Count == 0) return;

            int id = ++meshCounter;

            // Keep only the vertices this object's faces use, so its bounds cover the object and not the file so far.
            var meshVertices = new List<Vector3d>();
            var localIndex = new Dictionary<int, int>();
            var indices = new int[meshIndices.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                int v = meshIndices[i];
                if (v >= vertices.Count)
                {
                    throw new InvalidDataException($"OBJ object '{currentMeshName}': face vertex {v + 1} is outside the {vertices.Count} vertices read so far.");
                }

                if (!localIndex.TryGetValue(v, out int local))
                {
                    local = meshVertices.Count;
                    localIndex.Add(v, local);
                    meshVertices.Add(vertices[v]);
                }
                indices[i] = local;
            }

            doc.Elements.Add(new SceneElement { Id = id, ModelId = 1, Name = currentMeshName, Category = "OBJ" });
            doc.Geometries.Add(new SceneGeometry { Id = id, Vertices = meshVertices, Indices = indices });
            doc.Instances.Add(new SceneInstance { ElementId = id, GeometryId = id, Color = new RgbaColor(180, 180, 180), Name = currentMeshName });
            meshIndices.Clear();
        }
    }

    private static int ParseFaceVertex(string token, int totalVerts)
    {
        int slash = token.IndexOf('/');
        string numStr = slash >= 0 ? token.Substring(0, slash) : token;
        if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
        {
            if (idx > 0) return idx - 1; // 1-based
            if (idx < 0) return totalVerts + idx; // negative relative
        }
        return -1;
    }
}

namespace IOScene.Formats;

/// <summary>Writes placed scene geometry as Wavefront OBJ in metres with Z up.</summary>
public static class ObjSceneWriter
{
    /// <summary>Writes geometry to a UTF-8 OBJ file and replaces an existing file after a successful export.</summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="path">Path to the OBJ file.</param>
    public static void Save(SceneDocument scene, string path)
    {
        ArgumentNullException.ThrowIfNull(scene);
        SceneFormatFile.Replace(path, "obj", file =>
        {
            using var writer = new StreamWriter(file, leaveOpen: true);
            Write(scene, writer);
        });
    }

    /// <summary>Writes geometry to an OBJ text writer. The writer stays open.</summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="output">The OBJ text writer.</param>
    public static void Write(SceneDocument scene, TextWriter output) => WriteScene(scene, output, null, null);

    /// <summary>Writes OBJ geometry and an MTL library with the effective colours and opacity. Both writers stay open.</summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="output">The OBJ text writer.</param>
    /// <param name="materials">The MTL text writer.</param>
    /// <param name="materialFileName">File name of the adjacent MTL library, without a path or whitespace.</param>
    public static void Write(SceneDocument scene, TextWriter output, TextWriter materials, string materialFileName)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialFileName);
        if (ReferenceEquals(output, materials))
            throw new ArgumentException("OBJ and MTL require separate text writers.", nameof(materials));
        if (materialFileName is "." or ".." || materialFileName.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)
            || c is '/' or '\\' or ':' or '#'))
            throw new ArgumentException("Use a single MTL file name without paths, spaces or comments.", nameof(materialFileName));
        WriteScene(scene, output, materials, materialFileName);
    }

    private static void WriteScene(SceneDocument scene, TextWriter output, TextWriter? materials, string? materialFileName)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(output);
        if (scene.Validate().FirstOrDefault(issue => issue.Severity == SceneSeverity.Error) is { } error)
            throw new InvalidDataException($"Invalid scene ({error.Code}): {error.Message}");

        var elements = scene.Elements.ToDictionary(item => item.Id);
        var geometries = scene.Geometries.ToDictionary(item => item.Id);
        var materialColours = materials is null ? null : scene.Materials.ToDictionary(item => item.Id, item => item.Color);
        var colourNames = new Dictionary<RgbaColor, string>();
        long vertexOffset = 1;
        if (materialFileName is not null) output.WriteLine($"mtllib {materialFileName}");
        foreach (var instance in scene.Instances)
        {
            var geometry = geometries[instance.GeometryId];
            if (geometry.Vertices.Count == 0) continue;
            var name = instance.Name ?? elements[instance.ElementId].Name;
            string safeName = new(name.Select(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '#' or '\\' ? '_' : c).ToArray());
            output.WriteLine($"o {(safeName.Length == 0 ? "Element" : safeName)}");

            if (materials is not null)
            {
                var colour = instance.Color ?? (instance.MaterialId is int id ? materialColours![id] : RgbaColor.White);
                if (!colourNames.TryGetValue(colour, out string? materialName))
                {
                    materialName = FormattableString.Invariant($"material_{colourNames.Count + 1}");
                    colourNames.Add(colour, materialName);
                    materials.WriteLine($"newmtl {materialName}");
                    materials.WriteLine(FormattableString.Invariant($"Kd {colour.R / 255d:R} {colour.G / 255d:R} {colour.B / 255d:R}"));
                    materials.WriteLine(FormattableString.Invariant($"d {colour.A / 255d:R}"));
                    materials.WriteLine("illum 2");
                }
                output.WriteLine($"usemtl {materialName}");
            }

            var transform = instance.Transform ?? Transform3d.Identity;
            foreach (var vertex in geometry.Vertices)
            {
                var point = transform.Apply(vertex);
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                    throw new InvalidDataException("An OBJ vertex coordinate is outside the finite range.");
                output.WriteLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
            }

            int stride = geometry.Topology switch
            {
                SceneTopology.Triangles => 3,
                SceneTopology.Lines => 2,
                SceneTopology.Points => 1,
                _ => throw new InvalidDataException($"Unsupported OBJ topology: {geometry.Topology}."),
            };
            bool mirrored = stride == 3 &&
                Vector3d.Dot(Vector3d.Cross(ScaleAxis(transform.XAxis), ScaleAxis(transform.YAxis)), ScaleAxis(transform.ZAxis)) < 0;
            int count = geometry.Indices.Count == 0 ? geometry.Vertices.Count : geometry.Indices.Count;
            for (int i = 0; i < count; i += stride)
            {
                var record = stride switch
                {
                    3 => FormattableString.Invariant($"f {Index(i)} {Index(i + (mirrored ? 2 : 1))} {Index(i + (mirrored ? 1 : 2))}"),
                    2 => FormattableString.Invariant($"l {Index(i)} {Index(i + 1)}"),
                    _ => FormattableString.Invariant($"p {Index(i)}"),
                };
                output.WriteLine(record);
            }
            vertexOffset += geometry.Vertices.Count;

            long Index(int index) => vertexOffset + (geometry.Indices.Count == 0 ? index : geometry.Indices[index]);
        }
    }

    private static Vector3d ScaleAxis(Vector3d axis)
    {
        double scale = Math.Max(Math.Abs(axis.X), Math.Max(Math.Abs(axis.Y), Math.Abs(axis.Z)));
        return scale > 0 ? axis / scale : axis;
    }
}

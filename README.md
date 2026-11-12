# IOScene

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)

IOScene is a neutral scene model for tessellated BIM and CAD data, with a JSON file format and a structural
validator. It is the shared contract between the readers of source formats and the tools that show or check a model:
a reader turns its own model into a `SceneDocument`, and a viewer, a clash tool or a writer of another format takes
it from there. The package is pure managed .NET (8 and 10) and has no dependencies.

Built clean-room under the [IOExtend](https://github.com/IOExtend) organization.

**Status: preview.** The model is specified in [docs/contract.md][contract]; names and members may still change
before 0.1.0.

## What is in a scene

- Elements in a flat list with a parent link, a source id and an optional IFC GlobalId; an element may have no geometry.
- Geometry stored once and placed by instances with an affine transform; triangles, lines and points, indexed or not.
- Materials with colour, roughness, metalness and an optional texture.
- Typed property values with a display string, a number in SI units and a stable key; property sets are shared by id.
- Models (source files and links), levels, grids, saved viewpoints, 4D schedules and clash results.
- `SceneJson` reads and writes the file (`"Kind": "ioscene"`, `"Format": 1`), and `Validate()` checks the structure.

Everything is in metres, right-handed, Z up, in one coordinate space per document.

A scene holds what a viewer or a coordination tool shows, filters, measures or tests. It is not an authoring model:
parametric geometry, B-rep and type objects stay in the source formats' own libraries, and no source file is rebuilt
from a scene.

## Install

```sh
dotnet add package IOScene --prerelease
```

A package that depends on IOScene references one 0.x line, because a 0.x minor release may break the API and a patch
release never does. For the line 0.N the range is `[0.N.0, 0.N+1.0-0)`; the `-0` keeps out the previews of the next
line:

```xml
<PackageReference Include="IOScene" Version="[0.1.0, 0.2.0-0)" />
```

During the preview the lower bound is the preview: `[0.1.0-preview.2, 0.2.0-0)`. `dotnet pack` reports NU5104 for a
stable package with such a range; add `<NoWarn>$(NoWarn);NU5104</NoWarn>` to the referencing project.

## Quick start

One mesh into a scene file:

```csharp
using IOScene;

var scene = new SceneDocument { Title = "Quad" };
scene.AddMesh(
    "Quad",
    [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)],
    [0, 1, 2, 0, 2, 3],
    new RgbaColor(200, 80, 80));

foreach (var issue in scene.Validate())
    Console.WriteLine($"{issue.Severity} {issue.Code}: {issue.Message}");

SceneJson.Save(scene, "quad.json");
```

`AddMesh` creates the model, the geometry, the element and the instance that links them. A reader of a real format
fills the same lists directly: `Models`, `Geometries`, `Elements`, `Instances`, `PropertySets` and the rest.
`SceneJson.Load` brings the file back. `SceneJson.Write` and `SceneJson.Read` work on streams and on JSON text.

## Documentation

- [docs/contract.md][contract]: purpose and invariants, every type, the decisions behind them, what is left out,
  how the contract changes, the file format, the validation codes and the unit grammar.
- [docs/keys.md][keys]: the registry of property key namespaces and `SourceFormat` values.
- [CHANGELOG.md][changelog], [CONTRIBUTING.md][contributing] and [SECURITY.md][security].

## License

MIT, see [LICENSE][license].

[contract]: https://github.com/IOExtend/IOScene/blob/main/docs/contract.md
[keys]: https://github.com/IOExtend/IOScene/blob/main/docs/keys.md
[changelog]: https://github.com/IOExtend/IOScene/blob/main/CHANGELOG.md
[contributing]: https://github.com/IOExtend/IOScene/blob/main/CONTRIBUTING.md
[security]: https://github.com/IOExtend/IOScene/blob/main/SECURITY.md
[license]: https://github.com/IOExtend/IOScene/blob/main/LICENSE

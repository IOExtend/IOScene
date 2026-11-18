# Optional IOScene tools

`IOScene` defines the scene data and stores coordination results. `IOCoord` computes those results and consumes
the public IOScene 0.1 API. `IOScene.Formats` uses the same API for GLB and OBJ. The three projects share this
repository and can be installed separately. Both tools depend on IOScene, and IOScene has no dependencies.

The [tools preview release](https://github.com/IOExtend/IOScene/releases/tag/tools-v0.1.0-preview.1) includes both
packages. Install the tool your project uses:

```sh
dotnet add package IOCoord --version 0.1.0-preview.1
dotnet add package IOScene.Formats --version 0.1.0-preview.1
```

## GLB and OBJ

```csharp
using IOScene;
using IOScene.Formats;

var scene = SceneJson.Load("model.scene.json");
GlbSceneWriter.Save(scene, "model.glb");
ObjSceneWriter.Save(scene, "model.obj");
```

`GlbSceneReader.Load` reads embedded float32 positions and normals, unsigned indices and base material colours.
It flattens the node placements into one model and imports triangle, line and point primitives. Files declaring
required glTF extensions are rejected. The OBJ reader `ObjSceneReader.Load` groups faces by object and assumes
metres with Z up.

`ObjSceneWriter` bakes placed vertices into OBJ with Z up and writes triangle faces, line segments and points.
Its text-writer overload can also write an adjacent MTL library with effective colours and opacity.
The OBJ reader currently imports faces and ignores MTL libraries.

The writer emits [glTF 2.0 binary](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html) with the element tree,
indexed and non-indexed triangles, lines and points, shared mesh buffers, and placements with rotation, translation,
scale or reflection. The scene root rotates the metre-based geometry from Z up to glTF Y up. Materials carry
linear base colours, opacity and PBR factors. PNG and JPEG textures are embedded with their UV repeat scales.

The output contains geometry, transforms, appearance, element names, source ids and IFC GlobalIds.
The IOScene file is the source for property sets, schedules, saved viewpoints and clash results. The writer rejects
sheared placements, textures without UVs and coordinates outside the float32 range. It omits normals when a geometry
carries a degenerate normal so readers can generate their own.

## Measurements

```csharp
using IOCoord;
using IOScene;

var scene = SceneJson.Load("model.scene.json");
var measurement = SceneMeasure.PointToElement(scene, elementId: 42, new Vector3d(1, 2, 3));
```

`SceneMeasure` measures distances and path lengths in metres, angles in degrees, and the closest point on the
triangles, lines and points placed for an element. An element without geometry returns null. Element ids are local
to a scene.

## Clash checks, revision comparison, grids and BCF

`SceneClash.FindSurfaceIntersections` tests the placed triangle surfaces of two element selections. Its bounding
box hierarchy narrows the triangle pairs, and a surface check finds a contact point for each intersecting element
pair. It returns a `SceneClashTest` for export or review, with a contact tolerance of one nanometre.
`SceneClash.FindSurfaceClearances` returns the shortest gap between the placed triangle surfaces of each
element pair within an inclusive limit in metres. The result includes the distance and the midpoint of the
gap. Surface contacts report zero using the same tolerance.

`SceneClash.FindHardClashes` checks triangle contact and estimates depth from the minimum side of the overlap
of the two contacting mesh bounds, as in NwcIO. It reports negative depth in metres, skips positive depths
below the requested minimum, and keeps zero-depth contacts. `SceneClash.FindBoundingBoxOverlaps` uses the same
overlap metric and reports the centre of overlapping instance boxes without checking their surfaces. A box
overlap can include empty space. These two queries approximate depth using bounds.

`SceneClash.FindDuplicates` compares placed vertices and effective index order within a tolerance in metres.
It reports a pair when any two of its instances match, independent of material and display name.

`SceneClash.FindSolidContainments` finds a closed mesh that is fully enclosed by another closed mesh
without surface contact. It reports the contained element first. Meshes must have consistently oriented,
paired edges and no self-intersections. Reversed cavity shells represent empty space. The winding-number
test uses oriented triangle solid angles. [Triangle solid angle](https://doi.org/10.1109/TBME.1983.325207),
[generalized winding numbers](https://users.cs.utah.edu/~ladislav/jacobson13robust/jacobson13robust.pdf)

`SceneDiff.Compare` matches elements by model key and source id. It compares names and types, ordered property sets,
and ordered vertices and indices in scene coordinates. It reports elements without an identity separately and refuses
duplicate persistent ids. The comparison uses exact geometry values.

`SceneGrid.NearestLine` and `SceneGrid.NearestIntersection` query axes in the XY plane in metres. These methods use
the coordinates stored in the scene.

`BcfClashWriter.Write` writes existing `SceneClashTest` results as [BCF 2.1](https://github.com/buildingSMART/BCF-XML/blob/release_2_1/Documentation/README.md)
topics. The caller supplies the author and creation timestamp. Components use the scene's IFC GlobalIds. A viewpoint
with components carries selection and visibility. Camera positions and snapshots are outside this initial exporter.

## Build packages locally

```sh
dotnet pack IOScene.slnx -c Release -o ./artifacts
```

This builds the core and both optional preview packages for local testing. Existing RvtIO, NwcIO and viewer commands
continue to use their current implementations while replacement coverage is developed and verified.

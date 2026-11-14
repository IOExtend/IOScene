# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html) with one rule for versions before
1.0: a patch release adds or fixes, and a minor release may break the API and the file format.

## [Unreleased]

### Changed
- `docs/keys.md`: the namespace `nwc:` is registered for `NwcIO.Scene`; its formats are fixed from the first release of the package, 0.12.0. The `nwc` source format names `NwcIO.Scene` as its producer.

## [0.1.1] - 2026-11-13

### Fixed
- `SceneJson.CreateOptions()`: a scene of another kind or of a newer format inside a document read with these options
  is rejected with `InvalidDataException` also on a runtime without dynamic code; before, it came as a
  `TargetInvocationException` around it. `SceneJson.Read` and `SceneJson.Load` threw `InvalidDataException` there
  already; it is now the setter's exception itself, as with dynamic code, without the `TargetInvocationException` as
  its inner exception.
- `SceneJson.CreateOptions()`: a type read with these options that implements `IJsonOnDeserialized` and has a member
  that is a list of objects or strings gets its `OnDeserialized` call again, after the check for null entries; the
  options replaced it before.
- `SceneJson.CreateOptions()`: a member that a holder marks required (`[JsonRequired]` or `required`) follows the
  rules of the options like any other member, as the documentation of the method says: an explicit `null` is read as
  a missing member, and a `null` entry of its list is rejected. 0.1.0 left such members alone.
- README and `docs/contract.md`: a package that depends on IOScene references `[0.N.0, 0.N+1.0-0.0)` instead of
  `[0.N.0, 0.N+1.0-0)`. nuget.org refuses a package whose dependency bound is `0.2.0-0`, because a prerelease label of
  one part must start with a letter there; `0.2.0-0.0` has two parts and keeps out every preview of 0.2.0 except
  `0.2.0-0`, which IOScene does not publish.
- `docs/keys.md`: the `ifc:` formats are fixed from `IfcIO.Scene` 0.11.1, its first release; nuget.org refused 0.11.0.

### Changed
- `dotnet pack` compares the public API with 0.1.0 (package validation) and fails on a removed or changed member. CI
  also runs the tests as a runtime without dynamic code does (`-p:DynamicCodeSupport=false`).

## [0.1.0] - 2026-11-13

First stable release. The API, the file format (kind `ioscene`, format 1) and the validation codes are those of
0.1.0-preview.2. From this release a patch adds or fixes; a breaking change waits for 0.2.0 (`docs/contract.md`,
section 5). A package that depends on IOScene references `[0.1.0, 0.2.0-0)`.

### Changed
- README, `docs/contract.md` and SECURITY.md describe a stable package, and the install command has no
  `--prerelease`.
- `docs/contract.md` and the XML documentation state these rules; the code is unchanged:
  - An explicit `null` reads as the default for text other than `Kind`, lists, objects and members marked `?`.
    `SceneJson.Read` rejects a `null` for a number, an enum, a math value, a date-time or a property value
    with `InvalidDataException`.
  - `Validate()` does not check `vertex-count` and `index-count` for a topology it does not know; `index-range`,
    `normal-count` and `uv-count` apply to every topology.
  - A triangle's front is the side from which its vertices run counter-clockwise, and normals point to the front. An
    instance transform with a negative determinant reverses the winding (the glTF rule).
  - A `PlannedStart` or `PlannedEnd` at its default `0001-01-01T00:00:00+00:00` means "not given"; a consumer leaves
    such a task out of time-based views.
  - Date-times are written with an offset; a value without one is read in the local time zone.
  - `SceneModel.Producer` is the package id and the NuGet version without build metadata, separated by one space.
  - An instance colour replaces the material colour, also as the factor of the material's texture.
  - A lookup by key takes the first property with the key in the first set that has it.
  - `SceneJson` writes and reads JSON nested at most 128 levels deep, which holds 62 levels of tasks.
- `docs/keys.md`: the namespaces `revit:` and `ifc:` are registered; their formats are fixed from the first releases
  of `RvtIO.Scene` and `IfcIO.Scene`. Shared, project and family parameters have no key yet.
- The package has its own icon instead of a copy of the IOViewer icon.

## [0.1.0-preview.2] - 2026-11-12

First published preview of the scene contract. Names and members may still change before 0.1.0.

### Added
- The scene model: `SceneDocument` with models, elements, instances, geometries, materials, textures, property sets,
  levels, grids, viewpoints, schedules and clash tests. Metres, right-handed, Z up, one coordinate space per
  document; every cross-reference is by id.
- Geometry with a topology (triangles, lines, points), indexed or not, placed by instances with an affine transform.
- Typed property values (`SceneValue`) with a display string, a number in coherent SI and a unit, and `SceneUnits`
  with the unit grammar.
- Identity members: `SceneModel.Key`, `SceneElement.SourceId` and `SceneElement.IfcGuid`.
- `SceneDocument.Validate()` with stable diagnostic codes, `ComputeBounds()` and the `AddMesh` quick-start helper.
- `SceneJson`: scene JSON of kind `ioscene`, format 1, with stream and file methods. Math values are number arrays,
  vertex lists are flat, textures are base64. Files of another kind or of a newer format are rejected; unknown
  members are ignored. `SceneJson.CreateOptions()` gives the serializer rules of scene JSON to a document of another
  kind that holds a scene; the parents-first order of elements and the geometry size check of `Write` are not part
  of them.
- Math types `Vector2d`, `Vector3d`, `BoundingBox3d`, `Transform3d` and `RgbaColor`.
- `docs/contract.md` (the specification) and `docs/keys.md` (the registry of property key namespaces).

### Changed since 0.1.0-preview.1, which was not published
- `SceneClashResult.Distance` is `double?`: null means not reported, below 0 is the penetration depth and from 0 up
  the clearance, in metres. A missing `Distance` in a file reads as null.
- `Validate()` reports `id-range` for an element id outside `-(2^53 - 1)` to `2^53 - 1`, also where an element,
  instance, schedule task or clash names one, and checks a clash distance only when it is there.
- `SceneTaskType` and `SceneProjection` are open enums: a consumer treats an unknown value as `Construct` and as
  `Perspective`.
- The contract fixes colour (sRGB, straight alpha, colour times texture), texture coordinates, `Models[0]`, `ParentId`
  by `Kind`, the property lookup by key, the meaning of a null `Unit`, `SceneTask.Progress` from 0 to 1, grid line
  elevation and the frozen `IfcGuid` derivation. A per-instance index range is never added. Large vertex coordinates
  are allowed; a consumer rebases them before it narrows them to 32-bit floats.
- `docs/keys.md`: keys are compared whole and never parsed back, a lookup by key takes the first set, and
  `SourceFormat` is the id of the content format (`ifc` covers `.ifc` and `.ifczip`).

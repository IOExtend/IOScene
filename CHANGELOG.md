# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html) with one rule for versions before
1.0: a patch release is additive, and a minor release may break the API and the file format.

## [Unreleased]

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

# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html) with one rule for versions before
1.0: a patch release is additive, and a minor release may break the API and the file format.

## [0.1.0-preview.1] - not released yet

First preview of the scene contract. Names and members may still change before 0.1.0.

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
  members are ignored.
- Math types `Vector2d`, `Vector3d`, `BoundingBox3d`, `Transform3d` and `RgbaColor`.
- `docs/contract.md` (the specification) and `docs/keys.md` (the registry of property key namespaces).

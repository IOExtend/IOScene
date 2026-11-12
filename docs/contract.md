# The IOScene contract

This is the specification of the scene model in the `IOScene` package and of its JSON file (kind `ioscene`,
format 1). Producers (adapters that turn an engine's model into a scene) and consumers (viewers, coordination tools,
writers of other formats) are written against this text. The API reference is the XML documentation in the package.

Status: **preview**. Until `IOScene` 0.1.0 is released, names and members may still change.

Contents: [purpose and invariants](#1-purpose-and-invariants), [types](#2-types), [decisions by topic](#3-decisions-by-topic),
[what is not in the scene](#4-what-is-not-in-the-scene), [how the contract changes](#5-how-the-contract-changes),
[names](#6-names), [serialisation](#7-serialisation-and-versioning), [validation](#8-validation),
[units](#9-units).

## 1. Purpose and invariants

These never change.

- **Purpose rule.** The scene holds what a viewer or a coordination tool shows, filters, measures or tests. It never
  holds what an authoring tool edits. It is a one-way sink; no source file is rebuilt from it.
- Metres, right-handed, Z up, `double`, **one coordinate space per document**. Producers convert on the way in and
  report the factor. There is no per-document or per-model scale for a consumer to apply.
- Geometry stays local; large offsets go into the instance transform.
- `Id` values are scene-local and assigned by the producer. **Only `(SceneModel.Key, SourceId)` and `IfcGuid` may be
  written to anything that outlives the document** (BCF, clash statuses, diffs). Ids inside one scene file (schedule
  tasks, clash results) are valid for that file only.
- Category, type and level names are free strings in the vocabulary of the source. There is no enum of BIM classes.
- Every cross-reference is by id, never by list position.
- An element may have no geometry. Every instance belongs to an element; a producer with anonymous geometry creates
  one element for it.
- **Empty `Indices` means non-indexed: vertices are consumed in order by topology.** `Validate()` then checks the
  vertex count against the topology.
- Scope limit: in-memory lists. Point clouds above a few million points, streaming, tiling and levels of detail are
  outside this model, not "later". Small clouds use `Points`, non-indexed.

## 2. Types

Root namespace and assembly: `IOScene`. Lists of `SceneDocument` are shown in the order the file holds them.

```text
SceneDocument
  string Kind (= "ioscene")   int Format (= 1)   string Title
  List<SceneModel>        Models        // >= 1
  List<SceneMaterial>     Materials     List<SceneTexture> Textures
  List<ScenePropertySet>  PropertySets  // shared by id
  List<SceneLevel>        Levels
  List<SceneGeometry>     Geometries
  List<SceneElement>      Elements      // flat; the tree is ParentId
  List<SceneInstance>     Instances     // the only element <-> geometry link
  List<SceneGridSystem>   Grids         // list, each with ModelId
  List<SceneViewpoint>    Viewpoints
  List<SceneSchedule>     Schedules     // list, each with ModelId?
  List<SceneClashTest>    ClashTests    // results only
  BoundingBox3d ComputeBounds()   IReadOnlyList<SceneDiagnostic> Validate()
  SceneElement AddMesh(name, vertices, indices, color?)   // quick-start helper

SceneModel            // one source file, or one placement of a linked file
  int Id; string Key (unique in the scene, frozen derivation); string Name; string? Path
  string? SourceFormat ("rvt", "ifc", ...); string? Application, Author, CreatedAt
  string? SourceLengthUnit            // information only
  string? Producer                    // "RvtIO.Scene 0.5.0"

SceneElement
  long Id; int ModelId; string? SourceId; string? IfcGuid; long? ParentId; SceneNodeKind Kind
  string Name, Category, TypeName; int? LevelId; List<int> PropertySetIds (ordered)
SceneNodeKind  Element = 0, Group = 1, Model = 2, Level = 3        // open enum

SceneGeometry   int Id; SceneTopology Topology; Vertices; Indices (empty = non-indexed); Normals?; UVs?
SceneTopology   Triangles = 0, Lines = 1 (pairs), Points = 2        // open enum
SceneInstance   long ElementId; int GeometryId; Transform3d? Transform; int? MaterialId; RgbaColor? Color; string? Name

SceneMaterial   int Id; string Name; RgbaColor Color (alpha is the only opacity); double Roughness, Metalness; int? TextureId
SceneTexture    int Id; string MimeType; byte[] Data; double ScaleX, ScaleY

ScenePropertySet  int Id; string Name; string? Key; List<SceneProperty> Properties
SceneProperty     string Name; string? Key; SceneValue Value
SceneValue        (SceneValueKind Kind, string Text, double? Number, long? Integer, string? Unit)   // record struct
SceneValueKind    Text = 0, Boolean = 1, Integer = 2, Number = 3      // open enum
SceneUnits        constants ("m", "m2", "m3", "rad", "kg", "s", "K") and IsValid(unit)

SceneLevel        int Id; int ModelId; string Name; double? Elevation
SceneGridSystem   int ModelId; string Name; List<SceneGridLine> Lines     SceneGridLine  Name, Start, End, Elevation
SceneViewpoint    string? Id; string Name; SceneCamera Camera
SceneCamera       Position, Target, Up, FieldOfView (vertical, radians), AspectRatio, NearDistance, FarDistance,
                  SceneProjection Projection (Perspective = 0, Orthographic = 1)
SceneSchedule     int? ModelId; string Name; List<SceneTask> Tasks
SceneTask         string Id, Name; SceneTaskType TaskType (Construct = 0, Demolish = 1, Temporary = 2);
                  DateTimeOffset PlannedStart, PlannedEnd; DateTimeOffset? ActualStart, ActualEnd;
                  decimal? PlannedCost, ActualCost; double Progress; List<long> ElementIds; List<SceneTask> Children
SceneClashTest    string Name; List<SceneClashResult> Clashes
SceneClashResult  string Id, Name; long? Element1Id, Element2Id; string Element1Name, Element2Name; Vector3d Point;
                  double Distance; SceneClashStatus Status
SceneClashStatus  New = 0, Active = 1, Reviewed = 2, Approved = 3, Resolved = 4   // open enum
SceneDiagnostic   (SceneSeverity Severity, string Code, string Message, string? SourceId)   // record
SceneSeverity     Info = 0, Warning = 1, Error = 2
SceneJson         Write(doc, Stream), Read(Stream), Save(doc, path), Load(path); string overloads are conveniences
Math              Vector2d, Vector3d, BoundingBox3d, Transform3d, RgbaColor
```

`Transform3d` is stored by columns: `Apply(p) = XAxis * p.X + YAxis * p.Y + ZAxis * p.Z + Translation`.

## 3. Decisions by topic

| Topic | Decision |
|---|---|
| Meshes | Indexed or non-indexed triangles in `SceneGeometry`, optional normals and UVs. |
| Shared geometry and transforms | **One path only**: `SceneInstance` -> `SceneGeometry` with an affine `Transform3d` (mirror and non-uniform scale allowed; null is the identity). A unique mesh is a geometry used once. One level; producers flatten nested blocks, hotlinks and assemblies. |
| Lines, points | `Topology` on the geometry, so line work shares instancing (DWG blocks). A consumer that draws only triangles skips the rest and reports it. |
| Sub-range of a geometry per instance (`IndexStart`, `IndexCount`) | **Open until 0.1.0, then in or never.** It is the one item that cannot be added later without a break: a consumer that ignores it draws the whole geometry for every element. The default is "never"; it goes into 0.1.0 only if the object count of a real 2D drawing shows that one geometry per entity does not fit. |
| Curves | No. Producers tessellate with a tolerance option. |
| Text | Into properties now. A label list is additive later. Never glyph outlines. |
| Numeric id | `long Id`, scene-local. |
| Source id | `string? SourceId` scoped by `ModelId`: IFC GlobalId, Revit UniqueId, NWC node GUID, DWG handle as hex text. **Its derivation is frozen per producer** and covered by a golden identity test in the producer's repository; changing what a producer writes there is a breaking release of that producer. |
| Model key | **One derivation per producer, for good:** the caller's value when given (an option such as `ModelKey`), else the file name without directory; `#2`, `#3` in source order for repeated placements of one file. It never switches to a "better" id. A better id, when an engine learns to decode one, goes into a new optional member (`SceneModel.SourceGuid`) that consumers match first. `Validate()` rejects an empty or duplicate key. |
| Persisted identity | `(SceneModel.Key, SourceId)`. A `SceneModel` is one link **instance**. |
| IFC GlobalId | **`string? SceneElement.IfcGuid`** (the name BCF uses). For an IFC source it equals `SourceId`. A producer for another format fills it with the GlobalId its engine's IFC exporter writes for that element. BCF and scene -> IFC use this member and nothing else; a deterministic GUID from the persisted identity is the fallback for elements that have neither. It is a member and not a property because identity must not depend on a "parameter level" option or on loading the property store. |
| Tree | `ParentId` + `Kind` on the flat element list. One containment parent, chosen by the producer (an option such as "by level"). |
| Category, type | `Category` and `TypeName`, display strings. A stable machine class (`CategoryKey`) is an additive member with its first producer. |
| Level | `LevelId` into the `Levels` table (per model, with elevation). |
| Layer | Not in 0.1.0. `LayerId` on **`SceneInstance`** (entities inside one block sit on different layers) plus a `Layers` table are additive, with the first DWG producer. That producer drops off and frozen layers by default, so a consumer that ignores the table still shows correct output. |
| System, phase, workset, design option, host, room | Properties with registered keys. |
| Type objects | None. The parameters of a type are one `ScenePropertySet` referenced by every instance of the type. |
| Property values | Typed. `Text` is always the display string as the source shows it. `Number` is in coherent SI when `Unit` is set. `Integer` is a separate `long` and also carries `Boolean` as 0/1. A producer that cannot type a value writes `Kind = Text`; values are never guessed from strings. |
| Units | `Unit` follows one ASCII grammar ([section 9](#9-units)). `Validate()` warns on anything else. Null means dimensionless or not converted. |
| Property keys | `Key` on set and property is opaque to the scene and qualified by the producer (`revit:bip:-1001107`, `ifc:Pset_WallCommon.IsExternal`). **Registry:** [keys.md](keys.md) lists each namespace, its owner package and the key format, and the `SourceFormat` values. A registered key format changes only with a breaking release of its producer. Saved searches, clash rules and writers of other formats address keys; a viewer shows names. |
| Property sharing | Sets live in a document table and elements reference them by id. A geometry-free scene file and a property-free one are both valid documents, so a split export needs no property database; a join key (`SceneDocument.ExportId`) is additive when that is built. |
| Materials | Name, RGBA, roughness, metalness, optional texture by id. Opacity is stored once (alpha), with one optional colour per instance. |
| Coordinate frame, georeference | Not in 0.1.0. `SceneModel.Frame`, `ToShared` and a geo reference are additive members. They are a precondition of any consumer that tests two models against each other. |
| Links, federation | The `Models` table plus `ModelId` on elements, levels, grids and schedules. A merge helper that renumbers ids is added with the first federation consumer. No nested scenes. |
| Viewpoints | In the scene: optional `Id` (a saved-viewpoint GUID or a BCF viewpoint GUID), name, camera. For an orthographic camera the visible height is `2 * |Target - Position| * tan(FieldOfView / 2)`. Clip planes and visibility lists are additive. |
| 4D | `SceneSchedule` in the scene. |
| Clash results | `SceneClashTest`, `SceneClashResult` and `SceneClashStatus` in the scene: results only. Rules, tolerances, the engine and the store of statuses across runs belong to the coordination tool. Further fields of a source (assignee, comments, dates) are additive. |
| Conversion report | Each producer returns its own result object with `Document` and `Diagnostics`, by convention. There is no base class and no interface in 0.1.0. |

## 4. What is not in the scene

| Out | Lives in |
|---|---|
| Parametric and analytic geometry, B-rep, profiles, wall layers, openings, hosts, ports, type objects, schema matter | the engines' own models |
| Section planes and box, visibility, colour overrides, selection, redlines | the viewer |
| Clash rules, tolerances, the clash engine, statuses kept across runs | the coordination tool |
| 5D cost models, 4D dependency logic, exchange formats of one application | the engines |
| Options of any kind | extraction options in the engine, mapping options in the producer, encoding options in the writer, presets in the command line tool |
| `Extras` dictionaries | never on the element; format-specific element data is a property with a registered key |
| Sheets, viewports, linetypes, hatches, fonts, dimensions, nested instancing | nowhere (producers flatten or drop, and report) |
| Large point clouds, streaming, tiling, levels of detail, lossless round trip | outside this model |

## 5. How the contract changes

### Additive (a patch in 0.x, a minor from 1.0)

1. A new optional member whose default means "absent"; a new class; a new list on `SceneDocument`; a new method.
2. A new value of an open enum (`SceneNodeKind`, `SceneTopology`, `SceneValueKind`, `SceneClashStatus`).
3. A new `init` property on a record. **Never a new positional parameter**: it changes the constructor and
   `Deconstruct`, which is a binary break for every compiled producer. The planned `ElementRef` and `DateTime` value
   kinds put their payload into such properties.
4. A new file `Format` or a new file kind for the **same model** (for example a compact binary container). Producers
   never touch files, so no producer is locked out; every reader keeps reading all older formats.

**The test for 1 and 2 is "ignorable":** a consumer that does not know the addition still produces correct output for
everything it handled before. If ignoring it gives wrong output (a scale to apply, a range to honour, a new meaning of
an existing member), it is a break.

### Breaking (a 0.N+1 minor; never after 1.0 without 2.0)

Rename, retype, remove; change units, defaults or the meaning of an existing member (the meaning of empty `Indices`
included); reorder enum values; change a positional record signature.

A breaking minor is released only when the change is ready in every producer and consumer against one preview. Then
all packages go out in one session, in the order `IOScene`, engines with their producers, consumers.

### Breaking release of a producer (no `IOScene` change)

A change in how a producer derives `Key` or `SourceId`, or in a registered key format. The producer's golden identity
test fails first.

### Promotion rule

A fact starts as a property with a registered key. It becomes a member when two source formats can fill it and one
consumer shows, filters, measures or tests it. From then on:

- the member is authoritative;
- producers keep writing the old key until the next breaking `IOScene` minor;
- consumers read the member and fall back to the key through one helper that ships in `IOScene` with the member;
- `SceneModel.Producer` tells "absent in the source" from "the producer is older than the member".

Promotion is therefore additive by definition. It is the only allowed case of two ways to say one thing, and it ends
at the next breaking minor.

### Consumer obligations

They make the open enums safe:

- branch on `Topology`; skip and report a value you do not know;
- treat an unknown `SceneNodeKind` as `Group`;
- treat an unknown `SceneValueKind` as `Text` (its display string is always there);
- treat an unknown `SceneClashStatus` as `Active`.

The reader keeps enum numbers it does not know, and writes them back.

### Known additive items and their first user

| Item | First user or trigger |
|---|---|
| `SceneElement.CategoryKey` | the Revit producer, a writer of NWC |
| `SceneValueKind.ElementRef`, `DateTime` (payload in new `init` properties) | typed parameters of the Revit producer |
| `SceneModel.Frame`, `ToShared`, geo reference | a shared-coordinates option of a producer; required before two models are tested against each other |
| `SceneModel.SourceGuid` | the first engine that decodes a stable document or link-instance id |
| `SceneInstance.LayerId` + `Layers` table, text labels | the DWG producer |
| `SceneModel.ParentId` (nested links), merge helper | the first federation consumer |
| Named sets, clip planes on viewpoints, clash assignee and comments | the coordination tool, a writer of NWC, BCF |
| `SceneDocument.ExportId` | geometry and properties written as two files |
| Per-vertex colour | small point clouds |
| `ISceneReader` / `ISceneWriter` | an outside author asks for uniform discovery |

### Versions

In 0.x a patch release is additive and a minor release is breaking. A package that depends on `IOScene` 0.N references
the range `[0.N.0, 0.N+1.0-0)`; the `-0` keeps out the previews of the next breaking line. From 1.0 the range is
`[1.M.0, 2.0.0-0)`.

## 6. Names

- Root namespace and assembly `IOScene`. Every scene class has the `Scene` prefix. The root type is `SceneDocument`.
  **No type is ever named `Scene`**: inside a producer's code that identifier binds to the namespace
  `<Engine>.Scene`.
- A producer package is named `<Engine>.Scene`, its namespace is the package id, and it holds both directions
  (engine model -> scene, scene -> engine format).
- Producer types: `<Fmt>SceneReader`, `<Fmt>SceneWriter`, `<Fmt>SceneOptions`, `<Fmt>SceneResult`.

## 7. Serialisation and versioning

- `SceneJson` uses `System.Text.Json` and nothing outside the base class library. Member names are the property
  names in PascalCase, null members are omitted, enums are numbers. Text is UTF-8; characters outside ASCII are
  written as they are.
- **Root discriminator:** `"Kind": "ioscene"`, then `"Format": 1`. A viewer's own JSON has another kind, and a file
  without `Kind` is not a scene. The scene reader accepts only its own kind: it rejects a file without `Kind`, without
  `Format`, of another kind or of a newer format, in one pass over the stream.
- **Math values are number arrays:** `Vector2d` 2, `Vector3d` 3, `Transform3d` 12 (the three axes, then the
  translation), `RgbaColor` 4 integers from 0 to 255. **Vertex, normal and UV lists are one flat array each.** This
  is about 55 bytes per vertex against 69 for an array of objects, measured on 100,000 full-precision vertices. It is
  a saving, not a cure: text doubles dominate. The cure for size is a new encoding (item 4 of section 5), which is
  why `Format` is not tied to a breaking package version.
- The converters are attached to the types, so any `System.Text.Json` serialisation of a scene or of a math value
  gives the same encoding, for example a scene embedded in another document.
- **`Stream` is the primary API** (`Write(doc, Stream)`, `Read(Stream)`). The `string` overloads stay as
  conveniences; a .NET string caps a document at about a billion characters.
- Textures are `byte[]` in the model and base64 in the file.
- The writer emits tables in dependency order: models, materials, textures, property sets, levels, geometries,
  elements with parents first, instances, then grids, viewpoints, schedules and clash tests. Readers must not rely on
  it.
- Reader rules: unknown members are ignored; a missing optional member takes its default, and so does a `null` for a
  member that is never null; a `null` inside a list of objects is rejected; a newer `Format` is rejected with a clear
  message, never half-read. The reader does not validate: call `Validate()`.
- Numbers that are not finite cannot be written; `SceneJson.Write` throws. `Validate()` reports them first.
- `Format` counts file encodings of kind `ioscene`. It is bumped by a breaking model change and may also be bumped by a
  new encoding in an additive release. Golden files of every released `Format` are committed in
  `tests/golden/format-N` and read by every later version in tests.

A minimal file:

```json
{"Kind":"ioscene","Format":1,"Title":"Quad",
 "Models":[{"Id":1,"Key":"model","Name":"Quad"}],
 "Geometries":[{"Id":1,"Topology":0,"Vertices":[0,0,0,1,0,0,1,1,0,0,1,0],"Indices":[0,1,2,0,2,3]}],
 "Elements":[{"Id":1,"ModelId":1,"Name":"Quad"}],
 "Instances":[{"ElementId":1,"GeometryId":1,"Color":[200,80,80,255]}]}
```

## 8. Validation

`SceneDocument.Validate()` returns a list of `SceneDiagnostic`; an empty list means the scene is valid. Codes are
stable strings. Later versions may add codes.

| Code | Severity | Meaning |
|---|---|---|
| `no-model` | error | The scene has no model. |
| `duplicate-id` | error | Two models, elements, geometries, materials, textures, property sets or levels share an id. |
| `model-key` | error | A model key is empty or used by two models. |
| `dangling-model` | error | An element, level, grid system or schedule names a model that does not exist. |
| `dangling-level` | error | An element names a level that does not exist. |
| `dangling-set` | error | An element names a property set that does not exist. |
| `dangling-parent` | error | An element names a parent that does not exist. |
| `parent-cycle` | error | The parent chain of an element returns to itself. Reported once per cycle. |
| `dangling-element` | error | An instance, schedule task or clash names an element that does not exist. |
| `dangling-geometry` | error | An instance names a geometry that does not exist. |
| `dangling-material` | error | An instance names a material that does not exist. |
| `dangling-texture` | error | A material names a texture that does not exist. |
| `vertex-count` | error | A non-indexed geometry has a vertex count that its topology does not divide. |
| `index-count` | error | A geometry has an index count that its topology does not divide. |
| `index-range` | error | A geometry has an index outside its vertices. |
| `normal-count` | error | A geometry has normals, but not one per vertex. |
| `uv-count` | error | A geometry has texture coordinates, but not one per vertex. |
| `not-finite` | error | A vertex, normal, texture coordinate, instance transform or property number is NaN or infinite. |
| `duplicate-source-id` | warning | Two elements of one model share a source id. |
| `ifc-guid` | warning | An `IfcGuid` is not a 22-character IFC GlobalId (characters `0-9A-Za-z_$`, the first one `0` to `3`). |
| `unit-grammar` | warning | A property unit is outside the unit grammar. Reported once per unit. |

A geometry whose topology this version does not know is not checked for counts.

## 9. Units

`SceneValue.Unit` follows one ASCII grammar:

- symbols of coherent SI from a fixed list: `m`, `kg`, `s`, `A`, `K`, `mol`, `cd`, `rad`, `sr`, `Hz`, `N`, `Pa`, `J`,
  `W`, `C`, `V`, `F`, `ohm`, `S`, `Wb`, `T`, `H`, `lm`, `lx`;
- `.` for a product;
- at most one `/`; everything after it is the denominator;
- positive integer exponents written after the symbol, without a leading zero.

Examples: `m2`, `m3/s`, `kg/m3`, `N.m`, `W/m2.K`. There are no prefixes (`mm`, `kPa`), no `^`, no superscript digits,
no brackets and no other units (`ft`, `deg`). `Number` is converted to the coherent unit; `Text` keeps the value as the
source shows it, in the source's own unit. `SceneUnits.IsValid` checks a string, and `Validate()` warns with
`unit-grammar`.

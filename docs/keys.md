# Key registry

`ScenePropertySet.Key` and `SceneProperty.Key` are stable machine keys. The scene does not interpret them. A key is
qualified by a namespace that belongs to one producer package, so keys of different producers never collide. Saved
searches, clash rules and writers of other formats address properties by key; a viewer shows `Name`.

This file is the registry of the namespaces, of their key formats and of the `SceneModel.SourceFormat` values. See
[contract.md](contract.md) for the model itself.

## Rules

1. A key is `<namespace>:<rest>`. The namespace is lower-case ASCII letters and digits. The rest is defined by the
   owner of the namespace and is case-sensitive.
2. One namespace has one owner package. A new producer registers its namespace here, in the pull request that adds
   the producer, before its first release.
3. A registered key format is frozen. It changes only with a breaking release of the owner package, and the owner's
   golden identity test covers it.
4. A key names a fact of the source, not a display string: it does not change with the language of the source
   application or with a renamed parameter.
5. `Key` is optional. A property without a key is still shown; it cannot be addressed by a rule.
6. When a fact is promoted to a member of the model (the promotion rule of the contract), its key stays registered
   and producers keep writing it until the next breaking `IOScene` minor.

## Namespaces

| Namespace | Owner package | Status |
|---|---|---|
| `revit:` | `RvtIO.Scene` | planned; the formats below are fixed with the first release of the package |
| `ifc:` | `IfcIO.Scene` | planned; the formats below are fixed with the first release of the package |

No other namespace is registered. Producers for NWC, DWG and other formats register theirs when they are written.

### `revit:`

| Key | Used on | Meaning |
|---|---|---|
| `revit:instance` | property set | The instance parameters of an element. |
| `revit:type` | property set | The type parameters of an element; one set shared by every element of the type. |
| `revit:bip:<id>` | property | A built-in parameter, by its numeric id, which is negative: `revit:bip:-1001107`. |

Keys for shared, project and family parameters are registered with the first release of `RvtIO.Scene`.

### `ifc:`

| Key | Used on | Meaning |
|---|---|---|
| `ifc:<SetName>` | property set | A property set or quantity set by its IFC name: `ifc:Pset_WallCommon`. |
| `ifc:<SetName>.<PropertyName>` | property | A property or quantity of that set by its IFC name: `ifc:Pset_WallCommon.IsExternal`. |

## `SourceFormat` values

`SceneModel.SourceFormat` is the lower-case file extension of the source, without the dot.

| Value | Source | Producer |
|---|---|---|
| `rvt` | Revit project | `RvtIO.Scene` (planned) |
| `ifc` | IFC STEP file | `IfcIO.Scene` (planned) |
| `nwc` | Navisworks cache | `NwcIO.Scene` (later) |
| `dwg` | DWG drawing | `DwgIO.Scene` (later) |
| `glb` | binary glTF | `IOScene.Formats` (later) |
| `obj` | Wavefront OBJ | `IOScene.Formats` (later) |

A producer outside this list uses the extension of its format and adds a row here.

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
7. Keys are compared as whole strings (ordinal) and never parsed back.
8. A key may appear in several property sets of one element. A lookup by key takes the first set in
   `SceneElement.PropertySetIds` that has it, and the first property of that set with the key. Producers list
   occurrence (instance) sets before type sets.

## Namespaces

| Namespace | Owner package | Status |
|---|---|---|
| `revit:` | `RvtIO.Scene` | registered; the formats below are fixed from the first release of the package, 0.4.0 |
| `ifc:` | `IfcIO.Scene` | registered; the formats below are fixed from the first release of the package, 0.11.1 |
| `nwc:` | `NwcIO.Scene` | registered; the formats below are fixed from the first release of the package, 0.12.0 |

No other namespace is registered. Producers for DWG and other formats register theirs when they are written.

### `revit:`

| Key | Used on | Meaning |
|---|---|---|
| `revit:instance` | property set | The instance parameters of an element. |
| `revit:type` | property set | The type parameters of an element; one set shared by every element of the type. |
| `revit:bip:<id>` | property | A built-in parameter, by its numeric id, which is negative: `revit:bip:-1001107`. |

Shared, project and family parameters have no key yet. Their formats (for example `revit:shared:<guid>`) are
registered when `RvtIO.Scene` first writes them; that is an addition.

### `ifc:`

| Key | Used on | Meaning |
|---|---|---|
| `ifc:<SetName>` | property set | A property set or quantity set by its IFC name: `ifc:Pset_WallCommon`. |
| `ifc:<SetName>.<PropertyName>` | property | A property or quantity of that set by its IFC name: `ifc:Pset_WallCommon.IsExternal`. |

The property sets of an IFC type object, when they are read, use the same `ifc:` key form and come after the
occurrence sets.

### `nwc:`

| Key | Used on | Meaning |
|---|---|---|
| `nwc:<TabInternalName>` | property set | A Navisworks property tab by its internal name, for tabs whose internal name the Revit exporter fixes: `nwc:LcRevitData_Element`, `nwc:LcRevitData_Type`, `nwc:LcRevitMaterialProperties`, and `nwc:LcRevitId` (the element id). |
| `nwc:<TabInternalName>.<InternalName>` | property | A property of such a tab by its internal name, for names that do not change with language or a renamed parameter: built-in Revit parameters `lcldrevit_parameter_-<id>` (`nwc:LcRevitData_Element.lcldrevit_parameter_-1001107`), the exporter's `LcRevitPropertyElement{Category,CategoryId,Family,Id,Name,Type}`, Revit material asset names `revit_<Identifier>`, and `LcRevitId`. |

Other tabs and properties (references to other elements, rooms, and parameters whose internal name the exporter derives
from their display name) have no key. Keys for them are registered when `NwcIO.Scene` first writes them; that is an
addition.

## `SourceFormat` values

`SceneModel.SourceFormat` is the registered id of the format of the content, not of the container: `ifc` covers
`.ifc` and `.ifczip`, and `rvt` covers Revit project files.

| Value | Source | Producer |
|---|---|---|
| `rvt` | Revit project | `RvtIO.Scene` |
| `ifc` | IFC STEP file, also zipped (`.ifczip`) | `IfcIO.Scene` |
| `nwc` | Navisworks cache | `NwcIO.Scene` |
| `dwg` | DWG drawing | `DwgIO.Scene` (later) |
| `glb` | binary glTF | `IOScene.Formats` (later) |
| `obj` | Wavefront OBJ | `IOScene.Formats` (later) |

A producer outside this list registers an id for its format (usually the file extension) and adds a row here.

# IOScene tools changelog

## [0.1.0-preview.2]

- Rebuilds both tools against IOScene 0.1.2 and publishes the current `IOCoord` API, including the `SceneClash`
  hard-clash and surface-clearance entry points that consumers such as IOViewer call.
- `IOScene.Formats` GLB and OBJ import and export keep the scope of preview.1.

## [0.1.0-preview.1]

- `IOScene.Formats` reads embedded GLB and OBJ geometry and writes GLB and OBJ from an IOScene document.
- `IOCoord` provides measurements, surface clearance, hard clashes with an estimated depth, box overlap checks,
  duplicate geometry detection, solid containment, grid queries, revision comparison and BCF 2.1 export.
- Both packages use the public IOScene 0.1 API and can be installed independently.

See [Optional IOScene tools](tools.md) for supported inputs and scope.

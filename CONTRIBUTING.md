# Contributing to IOScene

IOScene is a contract: readers of source formats and the tools that consume a scene are compiled against it, in
other repositories. Read [docs/contract.md](docs/contract.md) before proposing a change to the model.

## Reporting bugs

Open a [GitHub issue](https://github.com/IOExtend/IOScene/issues) with:

- The IOScene package version.
- The API call used, with confidential values and personal paths removed.
- A minimal description of how to reproduce the problem, ideally a few lines of code that build the scene.
- Expected and actual results, including relevant error messages.

Do not attach scene files or models if they contain confidential or customer data.
A minimal description is better than sharing a confidential model.

Report security issues through GitHub private vulnerability reporting in
the Security tab. Do not open public issues for security vulnerabilities.

## Proposing changes

Open an issue first for any change to the model or the file format, so the scope and approach can be
discussed before implementation. Keep changes focused on the agreed problem.

## Development setup

Install the .NET SDK specified by `global.json`. It currently specifies
10.0.100, allows roll-forward to the latest feature band, and excludes
prerelease SDKs. Tests run on .NET 8 and .NET 10, so both runtimes are needed.

From the repository root, run:

```sh
dotnet build IOScene.slnx -c Release
dotnet test IOScene.slnx -c Release
```

The library is in `src/IOScene` and the tests in `tests/IOScene.Tests`. Tests use small scenes built in code and the
golden files in `tests/golden`.

To try an unreleased build in another repository, pack it into a local folder and pass that folder to the other
build:

```sh
dotnet pack IOScene.slnx -c Release -o <local-feed>
dotnet build <other solution> -p:RestoreAdditionalProjectSources=<local-feed>
```

Do not commit a `nuget.config` that points to a local feed.

## Changing the contract

The rules are in section 5 of [docs/contract.md](docs/contract.md). In short:

**Additive changes** (a patch release in 0.x, a minor release from 1.0):

- a new optional member whose default means "absent", a new class, a new list on `SceneDocument`, a new method;
- a new value of an open enum (`SceneNodeKind`, `SceneTopology`, `SceneValueKind`, `SceneClashStatus`);
- a new `init` property on a record, never a new positional parameter;
- a new file `Format` for the same model.

An addition must be ignorable: a consumer that does not know it still produces correct output for everything it
handled before. If ignoring it gives wrong output, it is a break.

**Breaking changes** (a 0.N+1 minor release; never after 1.0 without 2.0): rename, retype or remove a member; change
units, defaults or the meaning of an existing member; reorder enum values; change a positional record signature.

**Break procedure.** A breaking minor is tagged only when the pull request of every package that depends on IOScene
(the `<Engine>.Scene` packages and the consumers) is ready against the same preview on a local feed. Then all tags go
out in one session, in release order: IOScene, each engine, the consumers. Previews iterate on the local feed;
nuget.org gets the first preview and one release candidate.

Every change to the model comes with:

- the text in `docs/contract.md`, and `docs/keys.md` for a key namespace or a `SourceFormat` value;
- XML documentation on the new public API;
- tests, and for a new file `Format` a new folder `tests/golden/format-N`.

**Golden files are never regenerated.** `tests/golden/format-1` holds files written by the first version of format
1; every later version must read them and write the same bytes. A test that fails there means the change is not
additive.

## Clean-room policy

- Write all code independently. Do not use decompiled or leaked source code,
  proprietary headers, vendor SDKs or code copied from other libraries.
- The package has no dependencies beyond the .NET runtime libraries. Discuss any dependency first.
- Do not add customer models, confidential data, vendor files or binaries to
  the repository. Keep private test files outside version control.
- Do not put personal data in code, documentation, test data or commit
  metadata. Use your GitHub noreply address as the commit email.

## Code conventions

- Follow the surrounding code and repository formatting settings.
- Keep nullable reference types enabled.
- Warnings are treated as errors, a missing XML documentation comment on public API included.
- Write short English comments that explain why.
- The library writes nothing to the console and reads no environment.

## Pull requests

Include:

- A description of the problem and the change.
- Tests for behavior changes, including relevant errors and edge cases.
- The build and test commands run, their results, and any skipped checks.
- For a change to the model: whether it is additive or breaking, and why.

Write commit messages in the imperative mood, for example,
"Add a layer table for DWG producers".

## Releases

Push a tag matching the package version, such as `v0.1.0` or `v0.1.0-preview.1`, to run `release.yml`.
The workflow builds and tests the solution, publishes the NuGet package through Trusted Publishing and then attaches
the package, symbols, binaries, source and checksums to a GitHub release, which is marked as a prerelease when the tag
contains a hyphen. The release notes are the section of `CHANGELOG.md` for the version, so add that section before the
tag. The NuGet policy must name this repository, `release.yml`, the configured NuGet profile and the `IOScene*`
package pattern.

IOScene targets the lowest framework that any package depending on it targets, and drops a framework last.

## Licensing contributions

Contributions are licensed under the MIT license. By submitting a
contribution, you confirm that you have the right to contribute it under
that license.

## Questions

Ask questions in GitHub Discussions or repository issues.

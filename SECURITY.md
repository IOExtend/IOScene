# Security policy

## Supported versions

IOScene is at 0.x: a patch release adds or fixes, and only a minor release may break. Security fixes are made on the
latest release only.

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use GitHub private vulnerability reporting:
open the Security tab of the repository and choose "Report a vulnerability". Include the version of IOScene, the
API call, and a description of the problem. A sample file that triggers it helps, but do not send files with
confidential content.

You can expect an acknowledgement within a few days. Fixes are released as soon as they are verified, and the report
is credited unless you prefer otherwise.

## Scope

IOScene reads scene JSON files that can come from untrusted sources. Problems that matter most:

- crashes, hangs or unbounded memory use caused by a crafted scene file, in `SceneJson` or in `Validate()`;
- any behaviour of the library that executes code, contacts the network or writes outside the path it was given
  (the library does none of these; it writes only the file passed to `SceneJson.Save`, through a temporary file next
  to it that it then moves over that file).

The reader does not validate a scene. A consumer that takes files from untrusted sources calls `Validate()` and
checks the ids it follows.

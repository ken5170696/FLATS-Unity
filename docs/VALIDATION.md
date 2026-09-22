# Validation scope

Acceptance is in progress. This file does not claim a completed public release.

| Target / check | Status | Scope |
| --- | --- | --- |
| Unity 6000.3.24f1 initial import, Windows host | PASS | Fresh candidate import without copied Library or MCP package |
| Scene/prefab and regression checks | NOT RUN | Final candidate check pending |
| Final-commit clean clone | NOT RUN | Required before release |
| Windows x64 Mono build | NOT RUN | Required before release |
| Windows core gameplay and isolated persistence | NOT RUN | Required before release |
| Two-client Photon | NOT RUN | Requires configured service and two isolated clients |
| Web | NOT RUN | Separate browser acceptance required |
| Linux/macOS | NOT RUN | Native runtime hosts required |
| Android | NOT RUN | Device acceptance required |
| iOS | NOT RUN | macOS/Xcode and device required |

Build entry points preserve the existing platform capabilities; untested platforms are not certified by Windows results. Raw logs, screenshots and source-export audit records stay outside version control. A release must identify the tested commit, environment and actual operations. Historical acceptance reports are not evidence for this candidate.

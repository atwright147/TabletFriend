# Repository guidance

These instructions apply to the entire repository.

## Project and validation

- Tablet Friend is a Windows WPF application targeting .NET 10. Use the SDK selected by `global.json`.
- The solution is `TabletFriend/TabletFriend.sln`; application code is in `TabletFriend/TabletFriend`, and regression tests are in `TabletFriend/TabletFriend.Tests`.
- Follow the surrounding code style and keep changes focused on the requested task.
- Before reading or changing personal layouts, run `pwsh -NoProfile -File scripts/Resolve-UserFolders.ps1` under the intended Windows account. Use its verified `Documents` path for `TabletFriend\layouts`. If it reports `IdentityMismatch`, the shell is using a sandbox account even if `USERPROFILE` points at the user's profile; rerun under the user's account and do not infer Documents from `USERPROFILE` or `OneDrive`.
- For application changes, run `task test` or `dotnet test TabletFriend/TabletFriend.sln --configuration Release`. Add or update regression tests when behavior changes warrant them.
- For every new or updated layout YAML, validate it against `schemas/layout.schema.json` and Tablet Friend's runtime layout parser. The bundled-layout checks in `TabletFriend/TabletFriend.Tests/LayoutSchemaTests.cs` run with `task test`; extend those tests when changing layout syntax or validation rules so the schema and runtime behavior stay aligned.
- Follow `docs/testing.md` for desktop acceptance testing. Report any checks that could not be run.
- Documentation-only changes do not require application tests; check the diff for accuracy and whitespace errors with `git diff --check`.

## Changelog

- Before finishing a task, consider whether its changes need a changelog entry.
- Update `CHANGELOG.md` in the same change for user-visible features, bug fixes, behavior changes, configuration or compatibility changes, and significant build, runtime, or developer workflow changes.
- Put new entries under `## [Unreleased]`, using the existing `### Added`, `### Changed`, and `### Fixed` sections as appropriate. Add another Keep a Changelog category only when needed.
- Write concise entries describing the practical effect for users or contributors. Mention migration steps or new requirements when applicable.
- Amend an existing Unreleased entry when refining the same change instead of adding a duplicate. Check the implementation and diff rather than relying only on commit titles.
- Do not create a release version or date, or move entries out of Unreleased, unless explicitly asked to prepare a release. Preserve historical release entries unless correcting a documented error.
- Routine internal refactoring, test-only maintenance, typo fixes, and agent-instruction edits do not need entries unless they materially affect users or the development workflow. Avoid changelog entries that merely announce a changelog edit.

# Issue tracker: Linear

Issues and specs for this repo live in Linear: team **Sandeep** (key `SAN`), project **KAGE Unity** (`P-SAN-1`).
Use the Linear MCP tools (`mcp__linear-server__*`) for every operation. Issue ids look like `SAN-12`.

## Conventions

- **Create**: `save_issue` with team `SAN`, project `KAGE Unity`, a title and a Markdown description (real newlines, no escapes). Check `list_issues` with `query` for a duplicate first.
- **Read**: `get_issue SAN-<n>`, then `list_comments` for the discussion (`get_issue` doesn't include it).
- **List**: `list_issues` with project `KAGE Unity` plus `label` / `state` filters; follow `cursor` while `hasNextPage` is true.
- **Sub-issue**: `save_issue` with `parentId: SAN-<parent>`.
- **Comment**: `save_comment` on the issue.
- **Labels**: `save_issue` with `addLabels` / `removeLabels` (never `labels`, which replaces the whole set).
- **Close**: comment, then `save_issue` with state `Done` (or `Canceled` for wontfix, `Duplicate` with `duplicateOf`).
- **Blocking**: `blockedBy` / `blocks` relations on `save_issue`.
- Long agent-only detail (plans, file lists, logs) goes at the end of the description in a collapsible section that opens with `+++ Agent notes` and closes with `+++` on its own line.

## When a skill says "publish to the issue tracker"

Create a Linear issue in team `SAN`, project `KAGE Unity`.

## When a skill says "fetch the relevant ticket"

`get_issue` plus `list_comments`. The user normally passes the `SAN-<n>` id or a Linear URL.

## Wayfinding operations

Used by `/wayfinder`. The **map** is one issue; **child** tickets are its sub-issues.

- **Map**: an issue labelled `wayfinder:map` holding the Notes / Decisions-so-far / Fog body.
- **Child ticket**: a sub-issue of the map (`parentId`), labelled `wayfinder:<type>` (`research` / `prototype` / `grilling` / `task`).
- **Blocking**: native `blockedBy` relations. A ticket is unblocked when every blocker is Done, Canceled or Duplicate.
- **Frontier**: `list_issues` with `parentId` = the map, not completed, unassigned, with no open blockers; first in map order wins.
- **Claim**: `save_issue` assignee `me`, state `In Progress`, as the session's first write.
- **Resolve**: comment the answer, set state `Done`, then append a context pointer (gist + link) to the map's Decisions-so-far with a `patch` edit.

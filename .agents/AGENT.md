# AGENT.md: Operating rules for agents in the Imlight workspace

This file governs how agents work in this repository. Every rule was
verified against the tree and repo config on 2026-08-12. If a rule and the tree
disagree, **the tree wins**: update this file, don't churn the code.

## 1. Project snapshot

- **Repo:** `Revive101/Imlight`, an independent Wizard101 private server, written
  entirely in C#. Client target: **r801440** (modern). Mainline branch:
  `quality-assurance`.
- **Stack:** .NET 10 (`net10.0`), Akka.NET actors, RavenDB (player data), SpiralDB
  (world data), Imcodec submodule (wire types, generated from the client's own
  type system). See `README.md` and the docs book at `docs/docs/`.
- **Philosophy:** BYOD. The server distributes no copyrighted game data. Content
  lives in SpiralDB and the client's WADs; the server is a framework.

## 2. Sources of truth (in order)

1. The tree itself: observed practice beats any config file.
2. `.editorconfig`: formatting/naming rules for `dotnet format`.
3. `omnisharp.json`: IDE formatting. **Note:** it conflicts with `.editorconfig`
   on `else`/`catch`/`finally` newlines (see §10). The tree matches omnisharp.json.
4. `src/Imlight.sln.DotSettings`: ReSharper word dictionary only.
5. `docs/docs/`: the VitePress documentation book (`modules/imlight/*`).
6. Git history: conventional commits (`feat:`/`fix:`/`ref:`/`chore:`/`docs:`).

## 3. External references

- **Imcodec submodule:** pinned to `Revive101/Imcodec` main.

## 4. Code style: brackets & layout

- **K&R braces:** opening brace on the same line as the declaration/statement
  (`csharp_new_line_before_open_brace = none`; omnisharp `NewLinesForBraces* = false`).
- **`} else {` / `} catch {` / `} finally {` on one line.** Observed throughout the
  tree, e.g. `CombatCreatureAIComponent.cs:210`. Do not follow the editorconfig's
  `csharp_new_line_before_else = true`: it contradicts the tree (see §10).
- 4-space indentation, spaces not tabs; UTF-8; final newline; trailing whitespace
  trimmed (all per `.editorconfig`).
- **File-scoped namespaces** (`csharp_style_namespace_declarations = file_scoped`).
- **Usings:** `System.*` group first is the dominant practice; some newer files
  (e.g. `QuestService.cs`) sort them last. Match the file you are editing; never
  reorder a whole file's usings as a drive-by.
- Braces always used on control flow (`csharp_prefer_braces = suggestion`).
  Single-line blocks/statements are preserved, not folded.
- Expression-bodied members for one-liners; pattern matching and switch
  expressions preferred over if/else chains where equally readable.

## 5. Code style: naming & language features

- Private fields: `_camelCase`; static fields: `s_camelCase`; constants:
  PascalCase (per `.editorconfig`). Old SCREAMING_SNAKE constants exist
  (`PLANNING_TIME`); **do not churn them**: new constants are PascalCase.
- `var` everywhere (`csharp_style_var_* = true:silent`); predefined types
  (`int`, `string`) over BCL names.
- Default visibility `internal`; `sealed` where inheritance is not intended;
  accessibility modifiers always explicit (warning-level in `.editorconfig`).
- `readonly` on immutable fields (`dotnet_style_readonly_field = true:warning`).
- Modern construction: primary constructors, collection expressions (`[]`),
  object initializers, null propagation, `is null` / `is not null` checks.
- No `this.` qualification (suggestion level; the tree doesn't use it).

## 6. File headers: every .cs file gets the full template

Every existing `.cs` file in `src/` opens with the AGPL banner plus the section
block. New files must reproduce it exactly:

```
/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * SECTION NAME (ALL CAPS, matches the subsystem, e.g. "COMBAT DUEL SYSTEM")
 * ========================================================================
 * 
 * PURPOSE:
 * One or two sentences: what this type does and why it exists.
 * 
 * USAGE EXAMPLE:
 * How it is activated/consumed (e.g. "May activate from an NpcComponent…").
 * 
 * NOTE:
 * Non-obvious constraints, cross-references to related types, caveats.
 * May be empty.
 * 
 * TODO:
 * Open questions / unfinished work, as bullets. Not a fix list (see §7).
 * May be empty.
 * 
 * Created by: <author>
 * Version: KALI 1.0
 * Last Updated: MM/DD/YYYY
 */
```

Header rules:

- **`Created by:` is the file's original author and is never rewritten.** Files
  edited years later still say `Created by: Jooty`; that is the convention
  (`QuestService.cs`, `InteractReagentComponent.cs`). For a brand-new file, use
  the git author of its first commit (when I author on your behalf, your handle).
- **`Last Updated:`** is bumped to today's date (zero-padded `MM/DD/YYYY`, e.g.
  `04/01/2026`) whenever the file is substantively changed.
- `Version:` stays `KALI 1.0`; only the project bumps it.
- The `PURPOSE:`/`USAGE EXAMPLE:`/`NOTE:`/`TODO:` sections are terse. Empty
  sections stay empty (with a trailing space); that is the existing shape.

## 7. Comments: the rule

**Comments never fix problems.** A comment states intent, flags doubt, or asks an
open question. It never narrates a fix, a workaround, or a bug postmortem.

No em dashes in comments, or in any prose I author (file headers, docs, todos,
commit messages). If a break is needed, use a comma, a colon, or restructure the
sentence.

**Brevity is the default.** Most code needs no comment at all: a name that
explains itself is enough (e.g. `InteriorStowedMountId` needs no block above it).
Write a comment only when the code cannot carry the why (a subtle invariant, a
client behavior, a data convention). One line is the norm; two only when the why
truly needs it. No paragraphs on self-evident lines, ever.

- Good (the tree's own voice):
  - `// todo: is 0 female? Male?` (`FriendsService.cs:183`)
  - `// todo: this method is a mess.` (`CombatDuelSubCircle.cs:415`)
  - `// Todo: are there wards that increase incoming healing?` (`CombatResolver.cs:341`)
  - `// todo: (Jooty) I hate this. We need to find a better way to handle this.`
    (`WizardObjectLoader.cs:49`)
  - "Do we still need this?" A real comment is a question.
- Forbidden:
  - `// FIXME: X is broken, change Y to Z`: fix prescriptions.
  - Workaround narratives: "this is the invisible-mob lesson", "re-add here
    because the cull removes it", step-by-step bug postmortems inline.
  - TODO lists that read as fix instructions. `todo:` = *uncertainty*,
    not *assignment*.
- **Large comments are beaten out by smaller ones with better variable names.** The comment below is
  full of technical jargon that does little to explain the real issue:
  ```
  // Consecutive blocks always come from the sequential head. The free list only ever
  // holds single released ids in unordered order, so it cannot satisfy a contiguous
  // block; block allocation is only used during zone construction, before any release.
  ```
  Names should carry the mechanics; a comment that needs three lines of jargon is a sign the names
  are wrong.
- Lowercase `// todo:` is the dominant form; use it.
- **No XML summaries on private methods.** If a private method genuinely needs explanation, put a `//`
  comment block inside the body instead. Internal helpers likewise; public API such as protocol messages
  and services keeps its summaries.
- **Summaries are never one-liners.** Not even
  `/// <summary>Reserves <paramref name="count"/> consecutive ids, or throws if unavailable.</summary>`.
  The tags always go on their own lines:
  ```
  /// <summary>
  /// Reserves <paramref name="count"/> consecutive ids, or throws if unavailable.
  /// </summary>
  ```
- **A comment above a function definition that is not a `///` summary is never, never allowed:**
  ```
  // NOTE: must be protected — reflection-based handler registration on derived entity types
  // (CombatMinionEntity) cannot see a private base-class method.
  [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITYRSP))]
  protected void ReceiveComponentIdentity(ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITYRSP message) {
  ```
  `///` summaries are fine on public/internal API; anything else goes inside the method body, after the
  opening brace. A comment sitting above a method signature is noise at best and stale at worst, since
  the signature it describes changes independently of the body.
- **Never explain a system at a variable definition.** The comment below narrates a whole load
  handshake over two field declarations:
  ```
  // Asynchronous entity-load handshake state. A load batch records who asked for the results and
  // how many entities are still initializing; when the count drains, the supervisor reports done
  // without ever blocking on Ask().Result.
  private IActorRef _loadSender;
  private int _pendingEntityLoads;
  ```
  The why belongs where the state is used; the names should carry the rest.
- The header's `TODO:` section follows the same rule: open questions
  ("Implementation of creature stunning functionality"), not fixes.
- Comments explain *why* or *doubt*, never restate the code (`// Delete the
  creature.` is fine; `// Increment i by 1` is not).

## 8. Architecture idioms: match, don't invent

- **Services** (`Game/Services/`): `internal class XService(SessionActor sessionActor)
  : MessageService(sessionActor)`; each handler is a private method tagged
  `[MessageHandler(typeof(PROTOCOL_XXX_PROTOCOL.MSG_Y))]`; access the player via
  `GetActiveWizard()`, reply via `SendToSocket(...)`; `protected static Props(...)`
  factory for the Akka actor.
- **Zone components** (`Game/Zone/Components/`): `internal sealed class
  XComponent(ZoneEntity entity) : ZoneEntityComponent(entity)` plus
  `IComponentFactory` (and `IServiceComponent` when they expose an interaction),
  with `ServiceName` / `NpcIcon` properties.
- **Akka:** actors created via `Props` factories; actor refs live in `s_` fields;
  inter-server messages are the protocol types (`SERVER_100_PROTOCOL.*`).
- **Logging:** `Logger.Information("...{Placeholder}...", Logger.Args(...))` uses
  named placeholders with `Logger.Args`, never string interpolation or `+`.
- **Data:** static world content (templates, drops, quests) comes from SpiralDB /
  WADs; player-generated data lives in `WizardData` (RavenDB) collections.
  Small lookup tables are `internal static class` registries, loaded at boot.
- **Wire types** come from Imcodec's generated code
  (`Imcodec.MessageLayer.Generated`). Never hand-roll protocol structs.

## 9. Commits & PRs

- Conventional commits: `feat:`, `fix:`, `ref:`, `chore:`, `docs:` (per README
  contributing notes).
- Feature branches merge into `quality-assurance` (merge style: `` `Branch` ->
  `quality-assurance` (#NN) ``).
- Milestone commits use the KALI scheme: `KALI 26Q2.14c | Feature (#NN)`.
- One item, one PR: no drive-by reformatting, no scope creep.

## 10. Known conflicts & quirks (don't "fix" them)

- `.editorconfig` says `csharp_new_line_before_else = true`; `omnisharp.json`
  and the whole tree say `} else {` on one line. **Tree wins.** (Flagging it
  upstream is fine; mass-reformatting is not.)
- `using` order varies (System-first vs System-last); match the file.
- Constant casing varies (SCREAMING vs PascalCase); match the file, PascalCase
  for new ones.
- `src/.idea/` contains 3 tiny tracked IDE-local files (`.gitignore`, `.name`),
  harmless residue; leave alone unless asked.
- `.scratch/` is an empty scratch space for one-off work; don't commit to it
  without asking.

## 11. Scope discipline

- Execute the asked task; propose adjacent work in one sentence and wait.
- "Run it" means start, verify, report; no drive-by lint/refactor passes.
- If a plan would contradict the todo list, the todo list wins; if it would
  contradict this file, this file wins; update the file instead of working
  around it.

# Ignore rules

This fork lets you keep files and folders inside a sync root out of synchronization, the same way
`.gitignore` keeps files out of a Git repository.

## The two ignore files

**`.protonignore`** on the root of a sync folder. It applies to that whole sync root.

**`.gitignore`** in any directory. It applies to that directory and everything below it, exactly
where Git would apply it. This is on by default and can be turned off by constructing
`FileSystemIgnoreRuleProvider` with `honoursGitIgnoreFiles: false` in `SyncAgentFactory`.

Precedence, from weakest to strongest:

1. a shallower `.gitignore`
2. a deeper `.gitignore`
3. `.protonignore`

`.protonignore` wins last so that a negated pattern there can bring back something a repository
`.gitignore` excludes. Example: a repo ignores `dist/`, but you want it in the cloud anyway, so you
put `!dist/` into `.protonignore`.

The ignore files themselves are synced like any other file. Add them to their own rules if you do
not want that.

## Pattern syntax

The gitignore syntax, with Windows conventions where the two disagree.

| Pattern | Meaning |
| --- | --- |
| `build` | any file or folder named `build`, at any depth |
| `/build` | only `build` directly on the sync root |
| `build/` | only directories named `build` |
| `*.log` | any file ending in `.log`, at any depth |
| `!important.log` | re-include, overrides an earlier pattern |
| `docs/*.pdf` | contains a slash, so it is anchored to the sync root |
| `docs/**/draft.md` | `draft.md` anywhere below `docs`, including directly in it |
| `temp?` | `?` matches exactly one character, never a separator |
| `[Bb]in/` | character classes work |
| `# comment` | a comment |

Within one file the last matching pattern wins.

Differences from Git worth knowing:

- Matching is **case insensitive**, because the client only supports case insensitive file systems.
- Both `/` and `\` are accepted as separators. As a result, the backslash only works as an escape
  character for a leading `\#` and a leading `\!`.
- As in Git, a negated pattern cannot bring back an item out of an excluded directory. Once
  `secret/` is excluded, `!secret/public.txt` has no effect.

## What a rule does and does not do

An ignore rule keeps an item **out of** synchronization. It never takes an item **out of** the
cloud.

Concretely: if a rule matches an item the client has not indexed yet, that item is never uploaded.
If a rule matches an item the client already knows about, the rule is skipped and the item keeps
syncing. A line saying so is written to the log.

This is deliberate. Inside the sync engine, "the adapter no longer sees this item" and "the user
deleted this item" are the same event. Applying a new rule to already indexed data would be
reported as a deletion and would delete that data from Proton Drive on every other device. So the
practical rule is: **write the ignore file before you put the data into the sync folder.**

To apply a rule to something already synced, move it out of the sync folder, let the client
propagate the removal, then move it back.

Rules are evaluated on the local replica only. On the remote replica they are not applied at all,
since excluding a remote item would delete the local one.

## Reloading

Ignore files are cached. An unchanged file is checked at most once every two seconds, and re-read
whenever its size or last write time changes. There is no restart needed after editing one, but
items that were already indexed under the old rules are not revisited, see above.

## Where this lives in the code

| File | Role |
| --- | --- |
| `Sync.Shared/Ignore/IgnorePattern.cs` | one pattern, compiled to a regular expression |
| `Sync.Shared/Ignore/IgnoreRuleSet.cs` | the patterns of one ignore file, bound to its directory |
| `Sync.Shared/Ignore/FileSystemIgnoreRuleProvider.cs` | finds, reads and caches the ignore files |
| `Sync.Shared/Ignore/NullIgnoreRuleProvider.cs` | used for the remote replica |
| `Sync.Adapter/UpdateDetection/ItemExclusionFilter.cs` | built-in exclusions plus the user rules |
| `Sync.Adapter/Shared/SuccessStep.cs` | builds the relative path, applies the "never delete" rule |
| `Sync.Agent/SyncAgentFactory.cs` | wires the provider into the local adapter only |

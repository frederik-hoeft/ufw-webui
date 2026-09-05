# UFW mock design

## Problem and constraints

Local development currently depends on the real `ufw` executable. That makes the daemon's host-integration path awkward to exercise on Linux development machines and unavailable on Windows, where UFW does not exist. Running a real firewall also introduces privilege requirements and risks changing host networking during manual testing.

The mock must provide a drop-in development substitute for the UFW command-line boundary without entering the production trust path. Its observable command syntax, persisted firewall state, status output, mutation outcomes, and exit codes should match UFW closely enough that `Ufw.Systemd` can be pointed at the mock executable without any daemon code changes.

The compatibility target is the UFW 0.36.2 command surface documented by the supplied `ufw(8)` manual. UFW's host-dependent reports and application-profile inventory cannot be reproduced from a platform-neutral process without inspecting or modifying the host, so the mock gives those commands deterministic state-backed behavior instead of emulating Linux kernel/netfilter internals.

## Existing architecture

The production path has a useful boundary already:

- `Ufw.Systemd` owns privileged execution, UFW argument construction, UFW status parsing, mutation serialization, and reconciliation.
- `Ufw.Ipc.Shared` owns the normalized firewall-rule semantics used across process boundaries and by the daemon.
- `Ufw.Web` consumes daemon behavior over IPC and has no direct UFW dependency.

The mock therefore does not require a new shared library. It references `Ufw.Ipc.Shared` for the rule concepts already shared with `Ufw.Systemd`, while keeping mock-only CLI parsing, persistence, formatting, application profiles, and extended UFW protocol support in `Ufw.Mock`. No production project references the mock and no production implementation moves into it.

## Component model

`Ufw.Mock` is a normal .NET 10 console application built on ConsoleAppFramework. The entrypoint creates one `ConsoleAppBuilder`, configures the UFW global options, and registers command-category classes with chained `Add<T>()` calls. Command categories only translate CAF dispatch into a shared mock runtime; UFW's rule grammar is parsed by a dedicated parser rather than encoded as a large set of CAF method parameters.

The command categories are:

- lifecycle and policy commands: `enable`, `disable`, `reload`, `reset`, `default`, and `logging`;
- status/report commands: `status` and `show`;
- rule commands: `allow`, `deny`, `reject`, `limit`, `delete`, `insert`, and `prepend`;
- explicit rule commands below `rule`, matching UFW's optional `rule` keyword;
- routed rule commands below `route`;
- application-profile commands below `app`.

Global `--dry-run` and `--force` are defined as CAF global options so their placement and availability match UFW. CAF provides `-h`/`--help` and `--version`; the mock overrides the displayed version string to identify its UFW 0.36.2 compatibility target.

## State and persistence

The mock persists one versioned JSON document. The default path is platform-specific per-user application data under `Ufw.Mock/state.json`. `UFW_MOCK_STATE_PATH` overrides it, which allows tests and parallel development environments to isolate state without adding mock-only command-line switches that a real `ufw` invocation would reject.

The document contains:

- enabled/disabled state;
- logging level;
- default incoming, outgoing, and routed policies;
- default application-profile policy;
- whether IPv6 materialization is enabled;
- ordered concrete firewall rules;
- local application-profile definitions.

A missing state file is equivalent to a fresh UFW installation: disabled, incoming deny, routed deny, outgoing allow, application-profile policy `skip`, IPv6 enabled, and logging level `low`.

State changes are written through a temporary file and atomically replaced. `--dry-run` executes parsing and validation and produces the corresponding command result without replacing the state file.

## Rule model

Each persisted rule contains a normalized `FirewallRuleSpecification` from `Ufw.Ipc.Shared` plus mock-only UFW surface data that is intentionally outside the production semantic model:

- an extended protocol name for UFW protocols beyond `tcp`/`udp`;
- per-rule `log`/`log-all` mode;
- an optional application-profile name.

This composition lets normal TCP/UDP rules use exactly the same address, port, direction, action, and interface representation as the daemon while avoiding expansion of the production protocol contract solely to satisfy a development tool.

Family-neutral input is materialized as concrete IPv4 and IPv6 rules when IPv6 is enabled, matching UFW's visible numbered status rows. Explicit IPv4 or IPv6 addresses constrain a rule to that family. CLI family selection is resolved before shared address normalization so `0.0.0.0/0` and `::/0` retain the concrete family selected by the daemon even though the shared semantic normalizer represents both as `any`. Persisted/display ordering follows UFW's IPv4-first, IPv6-second numbering. Deleting by numbered status row removes only that concrete row; deleting by rule syntax removes all concrete family variants that match the requested rule. Routed rules intentionally reject `route delete NUM`, matching UFW.

Semantic duplicate detection excludes comments, consistent with the shared rule identity contract. Re-adding an existing rule with a different comment updates the comment. `insert` and `prepend` do not use that comment-update behavior. `insert NUM` interprets NUM in the global IPv4-first/IPv6-second numbered view and translates it to the selected family's persisted insertion point, while `prepend` remains family-local as in UFW.

## Rule grammar

The parser accepts UFW's simple and full rule syntaxes.

Simple rules support:

- optional `in`/`out` direction;
- optional interface selection;
- `log` and `log-all` in UFW's accepted pre-rule-body position;
- numeric ports and port/protocol forms;
- deterministic common service names such as `ssh`, `http`, `https`, `smtp`, `domain`, and `telnet`;
- application-profile names;
- comments.

Full rules support source/destination addresses, source/destination ports, independent source and destination application endpoints, protocol selection, interfaces, comments, and routed rules. Addresses are IP literals, CIDRs, or `any`. Numeric ports and ranges are validated using UFW's documented bounds and multi-port restrictions. The parser supports `tcp`, `udp`, `ah`, `esp`, `gre`, `vrrp`, `ipv6`, and `igmp`; protocols that cannot carry port clauses are rejected when ports are supplied, and IPv4-only protocols reject IPv6 rules. Route-direction clauses follow UFW's interface requirements rather than accepting direction keywords without a corresponding interface.

The parser normalizes fields through `RuleSpecificationNormalizer` before storing or comparing them. UFW's legacy `input`, `output`, and `forward` aliases are accepted for default-policy directions. Unknown commands, surplus positional arguments, unsupported combinations, and malformed syntax fail without changing state and use UFW-style `ERROR:` diagnostics with a non-zero exit code instead of inheriting ConsoleAppFramework's more permissive fallback behavior.

## Observable behavior

Status output is generated from persisted state and follows the column-oriented UFW format expected by the daemon's existing parser. `status numbered` prefixes the IPv4-first/IPv6-second concrete rows with current one-based display numbers. IPv6 rows use UFW's `(v6)` notation. `status verbose` includes logging and default-policy state before the same rule table. Persisted state is schema-validated on load so corrupt or unsupported state fails as a deterministic CLI error rather than leaking serializer/runtime failures.

Mutation messages distinguish enabled and disabled firewalls in the same way as UFW's user-facing command results (`Rule added` versus `Rules updated`, with IPv6 variants where applicable). Duplicate additions are skipped, rule comment updates are reported as updates, and nonexistent deletes fail rather than silently succeeding.

The reporting commands accept all reports documented by UFW. `added` and `user-rules` are derived from persisted rules. `raw`, built-in/before/after/logging reports, and `listening` are deterministic synthetic reports because the mock must not query the host's firewall tables or live sockets.

Application profiles are part of the state document instead of `/etc/ufw/applications.d`. `app list`, `app info`, `app default`, and `app update` operate on those definitions. UFW-visible edge behavior is retained, including rejecting `app update --add-new all` and using the singular `Port:` label for single-port profiles. This keeps the CLI portable and makes profile-based scenarios reproducible across Windows and Linux.

## Integration boundary

`Ufw.Systemd` already accepts an executable path through `ufw_path`. Development configurations can therefore point `ufw_path` directly at a published `Ufw.Mock` executable. The daemon continues to invoke the same argv, parse the same status representation, serialize mutations through the same execution gate, and reconcile state after mutation. The mock does not bypass any daemon behavior.

Because `AppSettings` validates that `ufw_path` names a file, framework-dependent `dotnet Ufw.Mock.dll` cannot be expressed as one executable plus arguments. Manual daemon integration should use an apphost produced by `dotnet build`/`dotnet publish`, which is directly executable on the development platform.

## Black-box test strategy

`Ufw.Mock.BlackboxTests` invokes the CAF application entrypoint with isolated `UFW_MOCK_STATE_PATH` values and captures console output. Tests assert only public CLI behavior and persisted effects, not internal parser or store implementation details.

Coverage includes the public CLI and daemon-facing compatibility cases, including:

- fresh-install and enabled status output;
- global enable/disable/logging/default behavior;
- simple and full rule additions;
- IPv4/IPv6 materialization, explicit-family all-addresses input, global numbering, and insertion;
- routed/interface rules, route deletion restrictions, and IPv4-only protocol constraints;
- delete-by-number and delete-by-rule behavior;
- insert/prepend ordering;
- duplicate/comment-update behavior;
- dry-run non-persistence;
- application-profile commands and independent source/destination profiles;
- deterministic report commands;
- invalid syntax, unknown commands, surplus arguments, malformed persisted state, and nonexistent rule failures;
- state continuity across separate application invocations.

The test project uses MSTest packages directly rather than the repository's `MSTest.Sdk` project SDK so it can be restored from the supplied offline package cache. This is a test-project packaging detail only; test code remains standard MSTest.

## Implementation plan

1. Add `Ufw.Mock` and `Ufw.Mock.BlackboxTests` to the solution without changing production project references.
2. Implement the versioned state model and atomic file store.
3. Implement rule parsing, validation, normalization, and concrete-family expansion.
4. Implement mutation/state services and UFW-compatible formatting.
5. Register the complete command facade through ConsoleAppFramework categories.
6. Add black-box coverage for command/state compatibility and the exact status surface consumed by the daemon.
7. Update steady-state architecture/development documentation with the development-only mock boundary and usage.
8. Validate the daemon-facing contract using the production argument builder and status parser unchanged on either side of the mock.

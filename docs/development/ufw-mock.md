# UFW mock

`Ufw.Mock` is a development substitute for the `ufw` executable. It lets the privileged-daemon path be exercised on Windows and on development machines where changing the real host firewall would be inconvenient or unsafe.

The mock sits at the same subprocess boundary as UFW. `Ufw.Systemd` does not know whether `ufw_path` names the real executable or the mock, so daemon parsing, mutation serialization, reconciliation, signed-intent verification, IPC, and web behavior remain production code. No production project depends on `Ufw.Mock`.

The compatibility target is the UFW 0.36.2 command surface used by this project. The goal is faithful behavior at the command boundary, not emulation of Linux netfilter internals. The separate `Ufw.Mock.IptablesSave` and `Ufw.Mock.Ip6tablesSave` executables emulate the read-only `iptables-save` and `ip6tables-save` process boundaries. They consume the same persisted model as `Ufw.Mock`, generate physical filter-table declarations, and support independent IPv4/IPv6 external rules. No production component links against the mock assemblies.

The daemon uses its existing `iptables_save_path`, `ip6tables_save_path`, `ufw_user_rules_path`, and `ufw_user6_rules_path` configuration to observe the mocks. `scripts/setup-dev.sh` selects built save frontends automatically when its UFW executable is `Ufw.Mock` (or `Ufw.Mock.exe` on Windows), and enables filter inventory when both are available. Explicit `IPTABLES_SAVE_PATH`, `IP6TABLES_SAVE_PATH`, `UFW_USER_RULES_PATH`, and `UFW_USER6_RULES_PATH` settings take precedence. Real deployments still observe the actual kernel filter table.

## Running it directly

Build the console application:

```bash
dotnet build src/Ufw.Mock/Ufw.Mock.csproj
dotnet build src/Ufw.Mock.IptablesSave/Ufw.Mock.IptablesSave.csproj
dotnet build src/Ufw.Mock.Ip6tablesSave/Ufw.Mock.Ip6tablesSave.csproj
```

Then invoke it like UFW. For example:

```bash
dotnet run --project src/Ufw.Mock -- status numbered

dotnet run --project src/Ufw.Mock -- allow 22/tcp

dotnet run --project src/Ufw.Mock -- route allow from 10.0.0.0/8 to 192.0.2.10 port 443 proto tcp
```

`--version` identifies both the UFW compatibility version and the fact that the process is the mock. Global `--dry-run` and `--force` follow the UFW command surface; command-specific validation still rejects combinations that real UFW would not accept.

For daemon development, point `Ufw.Systemd` at the built executable through its `ufw_path` setting. The daemon also reads UFW's defaults file independently to obtain IPv6 capability and default policies. `scripts/setup-dev.sh` handles both sides automatically on Windows: when a built mock is available it selects that executable and generates an `ufw_defaults_path` fixture with UFW's normal IPv6-enabled, deny-incoming/deny-routed/allow-outgoing defaults. Set `UFW_DEFAULTS_PATH` when a different development configuration is needed.

`Ufw.Systemd` passes its configured defaults-file path to the UFW subprocess as `UFW_DEFAULTS_PATH`. Real UFW ignores that development compatibility variable; `Ufw.Mock` consumes it so its IPv6 rule materialization and visibility use the same `IPV6` setting that the daemon reports to clients. The save frontends also honor `UFW_DEFAULTS_PATH` when present in their environment; otherwise they use the persisted IPv6 flag. Direct mock invocations without that variable retain the persisted mock-state IPv6 setting for standalone use. The mock JSON and defaults fixture remain separate storage concerns, but daemon-backed development no longer requires manually synchronizing their IPv6 flags.

## Persistent firewall state

The mock stores a versioned JSON state document rather than modifying the host firewall. By default it uses the current user's local application-data directory under `Ufw.Mock/state.json`. Set `UFW_MOCK_STATE_PATH` to isolate a development session or test run:

```bash
export UFW_MOCK_STATE_PATH="$PWD/artifacts/dev/ufw-mock-state.json"
```

A missing state file represents a fresh installation: the firewall is disabled, IPv6 support is enabled, incoming and routed traffic default to deny, outgoing traffic defaults to allow, and logging defaults to low.

State access is serialized with a companion lock file. Mutations write a temporary file and replace the state document atomically. `--dry-run` performs normal parsing and validation and emits the corresponding result without persisting the mutation.

Each persisted mutation also regenerates the corresponding `user.rules` and `user6.rules` translation files from the same state. On the first mock read, missing translation files are initialized, including for a fresh installation with no rules. By default, these files live beside `state.json`. Set `UFW_MOCK_USER_RULES_PATH` and `UFW_MOCK_USER6_RULES_PATH` to select other paths. These paths must agree with the daemon's `ufw_user_rules_path` and `ufw_user6_rules_path`. `scripts/setup-dev.sh` derives both daemon paths from `UFW_MOCK_STATE_PATH` (or the default per-user application-data location). If paths are overridden, export the matching mock variables in the environment of the daemon process, which launches the mock UFW subprocess. File replacements are atomic individually and serialized across mock invocations, but the three files are not an atomic snapshot relative to external readers; the daemon's existing best-effort cross-source reconciliation applies.

## iptables-save and ip6tables-save frontends

The two executables share the ConsoleAppFramework root command in `Ufw.Mock.Iptables`, including the state reader and filter-table construction. Execute the built platform-native apphosts directly; their names are `Ufw.Mock.IptablesSave[.exe]` and `Ufw.Mock.Ip6tablesSave[.exe]` under `src/artifacts/bin/<project>/debug/`. They accept `-t filter` (the daemon's invocation), optional `-c`, `--version`, and `--help`. Invalid arguments or unreadable/malformed fixture files cause a nonzero exit and a diagnostic on stderr, with no partial stdout. Neither executable invokes a firewall command, requires elevated privileges, nor changes the host firewall.

The synthetic `filter` table includes builtin INPUT, OUTPUT, and FORWARD chains, the UFW before/user/after chains, and supporting UFW limit and logging chains. Enabled UFW rules appear in their direction-specific user chain; disabled UFW rules remain persisted and in the translation files but are absent from the live save. IPv6 rules remain persisted when IPv6 support is disabled, but are omitted from the live IPv6 save until it is enabled again. Builtin policies follow the enabled mock's default policies (or ACCEPT when disabled); a default REJECT is represented by a terminal REJECT declaration with a DROP builtin fallback, since netfilter builtin policies cannot themselves be REJECT. The mock represents a practical development subset of UFW's rule expansion, not a byte-for-byte capture of a particular distro's iptables output or kernel counters.

External or ambient rules can be injected using one or more standard iptables-restore-style files (for instance, `before.rules`) per address family. Set `UFW_MOCK_IPTABLES4_BEFORE_FILES` / `UFW_MOCK_IPTABLES4_AFTER_FILES` or the IPv6 equivalents `UFW_MOCK_IPTABLES6_BEFORE_FILES` / `UFW_MOCK_IPTABLES6_AFTER_FILES`. Each variable accepts a list of paths separated by the platform's path-list separator (`:` on Unix; `;` on Windows). Files must have a complete `*filter` ... `COMMIT` section and may also contain other tables, which are ignored. The mock accepts chain declarations and `-A` rule declarations with their original options and target syntax; it combines these with the generated active filter table. Duplicate chain declarations must have identical policies. Rule declarations must name an existing chain. A rule added to `ufw-before-input` can jump to a custom chain defined in that file, while additions to INPUT/OUTPUT/FORWARD can model out-of-band host rules. The before/after distinction controls order within a particular chain relative to generated UFW rules. External rules are never included in the UFW translation files or numbered UFW status and therefore cannot automatically acquire managed provenance.

Example IPv4 fixture:

```text
*filter
:ufw-before-input - [0:0]
:custom-guard - [0:0]
-A ufw-before-input -s 198.51.100.0/24 -j custom-guard
-A custom-guard -p tcp --dport 22 -j DROP
COMMIT
```

For a fully isolated development session, run the daemon with `UFW_MOCK_STATE_PATH` set to a dedicated JSON path and configure its translation paths to that state file's sibling `user.rules` and `user6.rules`. The two save frontends inherit the same state-path environment as the UFW mock.


## What is modeled

The state model covers the UFW behavior consumed during development:

- enabled/disabled state, logging, and default policies;
- ordered IPv4 and IPv6 firewall rules;
- add, delete, insert, prepend, and routed-rule operations;
- comments, interfaces, directions, addresses, ports, and supported protocols;
- application profiles and application-profile rule expansion;
- `status`, `status numbered`, `status verbose`, and the UFW reports needed by the project.

Family-neutral rules materialize into concrete IPv4 and IPv6 rows when IPv6 is enabled and into IPv4 only when it is disabled. Explicit IPv6 additions are rejected while disabled. Like UFW 0.36.2, the mock retains previously persisted IPv6 rules but omits them from `status`, numbered deletion, and rule reports while `IPV6=no`; re-enabling IPv6 makes those stored rows observable again. Numbered status therefore exercises the same family and ordering assumptions as a real UFW installation. Positional insertion follows UFW's combined numbered CLI coordinate. IPv4 `insert N` positions must fall in the IPv4 prefix of `status numbered`; explicit IPv6 positions must fall in the IPv6 suffix, after which UFW converts the number to an IPv6-local position internally. Family-neutral insertion uses the selected combined anchor and mirrors placement into the other family when a corresponding rule exists. The mock preserves those semantics for ordinary and routed rules. Protocol-only rules retain their protocol marker in numbered status output. Duplicate and deletion behavior follows structural firewall semantics rather than assigning mock-only identities to rules.

Host-dependent reports that require inspecting real netfilter tables or live sockets are deterministic synthetic output. The mock deliberately does not inspect or alter host networking.

## Compatibility boundary

The mock is useful when the behavior under test begins at the UFW CLI or filter-observation subprocess boundary. It is not evidence that every UFW distribution/version formats every obscure rule identically. Parser or reconciliation work that depends on a particular UFW textual representation should still be checked against the supported real UFW version before treating mock behavior as authoritative.

The black-box test project exercises the mock through its process-style command surface and persistent state. When extending the mock, favor observable UFW compatibility over adding mock-specific command-line switches; test/development isolation belongs in environment-controlled state such as `UFW_MOCK_STATE_PATH`.

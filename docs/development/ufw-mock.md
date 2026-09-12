# UFW mock

`Ufw.Mock` is a development substitute for the `ufw` executable. It lets the privileged-daemon path be exercised on Windows and on development machines where changing the real host firewall would be inconvenient or unsafe.

The mock sits at the same subprocess boundary as UFW. `Ufw.Systemd` does not know whether `ufw_path` names the real executable or the mock, so daemon parsing, mutation serialization, reconciliation, signed-intent verification, IPC, and web behavior remain production code. No production project depends on `Ufw.Mock`.

The compatibility target is the UFW 0.36.2 command surface used by this project. The goal is faithful behavior at the command boundary, not emulation of Linux netfilter internals.

## Running it directly

Build the console application:

```bash
dotnet build src/Ufw.Mock/Ufw.Mock.csproj
```

Then invoke it like UFW. For example:

```bash
dotnet run --project src/Ufw.Mock -- status numbered

dotnet run --project src/Ufw.Mock -- allow 22/tcp

dotnet run --project src/Ufw.Mock -- route allow from 10.0.0.0/8 to 192.0.2.10 port 443 proto tcp
```

`--version` identifies both the UFW compatibility version and the fact that the process is the mock. Global `--dry-run` and `--force` follow the UFW command surface; command-specific validation still rejects combinations that real UFW would not accept.

For daemon development, point `Ufw.Systemd` at the built executable through its `ufw_path` setting. `scripts/setup-dev.sh` does this automatically on Windows when the mock has already been built and no `ufw` executable is available.

## Persistent firewall state

The mock stores a versioned JSON state document rather than modifying the host firewall. By default it uses the current user's local application-data directory under `Ufw.Mock/state.json`. Set `UFW_MOCK_STATE_PATH` to isolate a development session or test run:

```bash
export UFW_MOCK_STATE_PATH="$PWD/artifacts/dev/ufw-mock-state.json"
```

A missing state file represents a fresh installation: the firewall is disabled, IPv6 support is enabled, incoming and routed traffic default to deny, outgoing traffic defaults to allow, and logging defaults to low.

State access is serialized with a companion lock file. Mutations write a temporary file and replace the state document atomically. `--dry-run` performs normal parsing and validation and emits the corresponding result without persisting the mutation.

## What is modeled

The state model covers the UFW behavior consumed during development:

- enabled/disabled state, logging, and default policies;
- ordered IPv4 and IPv6 firewall rules;
- add, delete, insert, prepend, and routed-rule operations;
- comments, interfaces, directions, addresses, ports, and supported protocols;
- application profiles and application-profile rule expansion;
- `status`, `status numbered`, `status verbose`, and the UFW reports needed by the project.

Family-neutral rules materialize into concrete IPv4 and IPv6 rows when IPv6 is enabled, so numbered status output exercises the same family and ordering assumptions as a real UFW installation. Positional insertion follows UFW's family-local numbering: `insert N` addresses position `N` within the concrete IPv4 or IPv6 rule partition even though `status numbered` renders both partitions as one combined display sequence. The mock preserves that distinction for ordinary and routed rules. Protocol-only rules retain their protocol marker in numbered status output. Duplicate and deletion behavior follows structural firewall semantics rather than assigning mock-only identities to rules.

Host-dependent reports that require inspecting real netfilter tables or live sockets are deterministic synthetic output. The mock deliberately does not inspect or alter host networking.

## Compatibility boundary

The mock is useful when the behavior under test begins at the UFW CLI boundary. It is not evidence that every UFW distribution/version formats every obscure rule identically. Parser or reconciliation work that depends on a particular UFW textual representation should still be checked against the supported real UFW version before treating mock behavior as authoritative.

The black-box test project exercises the mock through its process-style command surface and persistent state. When extending the mock, favor observable UFW compatibility over adding mock-specific command-line switches; test/development isolation belongs in environment-controlled state such as `UFW_MOCK_STATE_PATH`.

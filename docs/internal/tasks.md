# Open tasks

This file is temporary, non-normative working storage for unresolved design and implementation work. Remove completed items after they reach the approved baseline; steady-state behavior belongs in the permanent architecture, protocol, deployment, development, or testing documentation.

## Signed UFW presentation consistency check

Evaluate whether a future signed-intent protocol revision should include the canonical UFW rule text shown to the user and require the daemon to compare that signed presentation with the text rendered from the authoritative structural rule. Treat this only as a defense-in-depth consistency assertion; validated structural fields and direct argv execution remain the command-injection boundary. Any signed-intent payload/version change requires separate security design and approval.

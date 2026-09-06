using System.Collections.Immutable;

namespace Ufw.Firewall;

/// <summary>
/// Canonical UFW rule syntax represented both as validated rule argv tokens and as human-readable command text.
/// </summary>
/// <remarks>
/// <see cref="Arguments"/> contains the rule expression itself; callers may prepend execution-only UFW options such as <c>--force</c>.
/// <see cref="DisplayText"/> is for presentation only. Subprocess execution must use <see cref="Arguments"/> directly and must not parse or execute the display text through a shell.
/// </remarks>
public sealed record UfwRenderedRule(ImmutableArray<string> Arguments, string DisplayText);

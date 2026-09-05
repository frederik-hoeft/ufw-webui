using System.Globalization;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Mock.Formatting;
using Ufw.Mock.Rules;
using Ufw.Mock.State;

namespace Ufw.Mock.Cli;

internal sealed class UfwCommandExecutor(UfwGlobalOptions options)
{
    private static readonly HashSet<string> s_reports = new(StringComparer.OrdinalIgnoreCase)
    {
        "raw",
        "builtins",
        "before-rules",
        "user-rules",
        "after-rules",
        "logging-rules",
        "listening",
        "added",
    };

    private readonly UfwStateStore _store = new();
    private readonly UfwRuleParser _parser = new();

    public int Enable(IReadOnlyList<string> arguments) => Execute(() =>
    {
        RequireNoArguments(arguments, "ufw enable");
        _store.Update(options.DryRun, state =>
        {
            state.Enabled = true;
            return 0;
        });
        Console.WriteLine("Firewall is active and enabled on system startup");
        return 0;
    });

    public int Disable(IReadOnlyList<string> arguments) => Execute(() =>
    {
        RequireNoArguments(arguments, "ufw disable");
        _store.Update(options.DryRun, state =>
        {
            state.Enabled = false;
            return 0;
        });
        Console.WriteLine("Firewall stopped and disabled on system startup");
        return 0;
    });

    public int Reload(IReadOnlyList<string> arguments) => Execute(() =>
    {
        RequireNoArguments(arguments, "ufw reload");
        return _store.Read(state =>
        {
            Console.WriteLine(state.Enabled ? "Firewall reloaded" : "Firewall not enabled (skipping reload)");
            return 0;
        });
    });

    public int Reset(IReadOnlyList<string> arguments) => Execute(() =>
    {
        RequireNoArguments(arguments, "ufw reset");
        if (!options.Force && !Confirm("Resetting all rules to installed defaults. Proceed with operation (y|n)? "))
        {
            Console.WriteLine("Aborted");
            return 0;
        }

        _store.Update(options.DryRun, state =>
        {
            UfwMockState defaults = UfwMockState.CreateDefault();
            state.SchemaVersion = defaults.SchemaVersion;
            state.Enabled = defaults.Enabled;
            state.LoggingLevel = defaults.LoggingLevel;
            state.DefaultIncomingPolicy = defaults.DefaultIncomingPolicy;
            state.DefaultOutgoingPolicy = defaults.DefaultOutgoingPolicy;
            state.DefaultRoutedPolicy = defaults.DefaultRoutedPolicy;
            state.DefaultApplicationPolicy = defaults.DefaultApplicationPolicy;
            state.IPv6Enabled = defaults.IPv6Enabled;
            state.Rules.Clear();
            return 0;
        });
        Console.WriteLine("Resetting all rules to installed defaults");
        return 0;
    });

    public int SetDefault(IReadOnlyList<string> arguments) => Execute(() =>
    {
        if (arguments.Count is < 1 or > 2)
        {
            throw Error("Usage: ufw default allow|deny|reject [incoming|outgoing|routed]");
        }

        string policy = arguments[0].ToUpperInvariant() switch
        {
            "ALLOW" => "allow",
            "DENY" => "deny",
            "REJECT" => "reject",
            _ => throw Error($"Invalid default policy '{arguments[0]}'."),
        };
        string direction = arguments.Count == 2
            ? arguments[1].ToUpperInvariant() switch
            {
                "INCOMING" or "INPUT" => "incoming",
                "OUTGOING" or "OUTPUT" => "outgoing",
                "ROUTED" or "FORWARD" => "routed",
                _ => throw Error($"Invalid default direction '{arguments[1]}'."),
            }
            : "incoming";

        _store.Update(options.DryRun, state =>
        {
            switch (direction)
            {
                case "incoming":
                    state.DefaultIncomingPolicy = policy;
                    break;
                case "outgoing":
                    state.DefaultOutgoingPolicy = policy;
                    break;
                case "routed":
                    state.DefaultRoutedPolicy = policy;
                    break;
            }
            return 0;
        });

        Console.WriteLine($"Default {direction} policy changed to '{policy}'");
        Console.WriteLine("(be sure to update your rules accordingly)");
        return 0;
    });

    public int SetLogging(IReadOnlyList<string> arguments) => Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw Error("Usage: ufw logging on|off|low|medium|high|full");
        }

        string level = arguments[0].ToUpperInvariant() switch
        {
            "ON" => "on",
            "OFF" => "off",
            "LOW" => "low",
            "MEDIUM" => "medium",
            "HIGH" => "high",
            "FULL" => "full",
            _ => throw Error($"Invalid log level '{arguments[0]}'."),
        };

        string effectiveLevel = _store.Update(options.DryRun, state =>
        {
            if (level == "on")
            {
                if (state.LoggingLevel == "off")
                {
                    state.LoggingLevel = "low";
                }
            }
            else
            {
                state.LoggingLevel = level;
            }
            return state.LoggingLevel;
        });

        Console.WriteLine(effectiveLevel == "off" ? "Logging disabled" : "Logging enabled");
        return 0;
    });

    public int Status(IReadOnlyList<string> arguments) => Execute(() =>
    {
        bool verbose = false;
        bool numbered = false;
        if (arguments.Count > 1)
        {
            throw Error("Usage: ufw status [verbose|numbered]");
        }
        if (arguments.Count == 1)
        {
            if (arguments[0].Equals("verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
            }
            else if (arguments[0].Equals("numbered", StringComparison.OrdinalIgnoreCase))
            {
                numbered = true;
            }
            else
            {
                throw Error($"Unknown status mode '{arguments[0]}'.");
            }
        }

        return _store.Read(state =>
        {
            Console.WriteLine(UfwOutputFormatter.FormatStatus(state, numbered, verbose));
            return 0;
        });
    });

    public int Show(IReadOnlyList<string> arguments) => Execute(() =>
    {
        if (arguments.Count != 1 || !s_reports.Contains(arguments[0]))
        {
            throw Error("Usage: ufw show raw|builtins|before-rules|user-rules|after-rules|logging-rules|listening|added");
        }

        string report = arguments[0].ToUpperInvariant();
        return _store.Read(state =>
        {
            string output = report switch
            {
                "ADDED" => UfwOutputFormatter.FormatAdded(state),
                "USER-RULES" => UfwOutputFormatter.FormatUserRules(state),
                "LISTENING" => "Netid  State  Local Address:Port  Peer Address:Port  Process\n# Ufw.Mock does not inspect host sockets.",
                _ => $"# Ufw.Mock synthetic {report} report\n# Firewall status: {(state.Enabled ? "active" : "inactive")}\n# Host netfilter tables are not inspected.",
            };
            Console.WriteLine(output);
            return 0;
        });
    });

    public int Add(FirewallAction action, IReadOnlyList<string> arguments, bool routed) =>
        Execute(() => MutateRule(action, arguments, routed, RulePlacement.Append, null));

    public int Insert(IReadOnlyList<string> arguments, bool routed) => Execute(() =>
    {
        if (arguments.Count < 2 || !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            throw Error("Insert requires a one-based rule number followed by a rule.");
        }
        FirewallAction action = ParseAction(arguments[1]);
        return MutateRule(action, arguments.Skip(2).ToArray(), routed, RulePlacement.Insert, number);
    });

    public int Prepend(IReadOnlyList<string> arguments, bool routed) => Execute(() =>
    {
        if (arguments.Count < 1)
        {
            throw Error("Prepend requires a rule.");
        }
        FirewallAction action = ParseAction(arguments[0]);
        return MutateRule(action, arguments.Skip(1).ToArray(), routed, RulePlacement.Prepend, null);
    });

    public int Delete(IReadOnlyList<string> arguments, bool routed) => Execute(() =>
    {
        if (arguments.Count == 0)
        {
            throw Error("Delete requires a rule or rule number.");
        }

        if (arguments.Count == 1 && int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int displayNumber))
        {
            if (routed)
            {
                throw Error("'route delete NUM' unsupported. Use 'delete NUM' instead.");
            }
            return DeleteByNumber(displayNumber);
        }

        FirewallAction action = ParseAction(arguments[0]);
        string[] ruleArguments = [.. arguments.Skip(1)];
        List<UfwMockRule> removed = _store.Update(options.DryRun, state =>
        {
            ParsedRuleRequest request = _parser.Parse(action, ruleArguments, routed, state);
            IReadOnlyList<UfwMockRule> targets = request.Materialize(state.IPv6Enabled);
            List<UfwMockRule> matches = [];
            foreach (UfwMockRule target in targets)
            {
                UfwMockRule? match = state.Rules.FirstOrDefault(existing => UfwRuleComparer.SemanticallyEqual(existing, target));
                if (match is not null)
                {
                    matches.Add(match);
                }
            }
            if (matches.Count == 0)
            {
                throw Error("Could not delete non-existent rule");
            }
            foreach (UfwMockRule match in matches)
            {
                state.Rules.Remove(match);
            }
            return matches;
        });

        bool enabled = _store.Read(static state => state.Enabled);
        foreach (UfwMockRule rule in removed)
        {
            Console.WriteLine(FormatMutationMessage(enabled, "deleted", rule.Specification.AddressFamily));
        }
        return 0;
    });

    public int AppList(IReadOnlyList<string> arguments) => Execute(() =>
    {
        RequireNoArguments(arguments, "ufw app list");
        return _store.Read(state =>
        {
            Console.WriteLine("Available applications:");
            foreach (UfwApplicationProfile profile in state.ApplicationProfiles.OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("  " + profile.Name);
            }
            return 0;
        });
    });

    public int AppInfo(IReadOnlyList<string> arguments) => Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw Error("Usage: ufw app info PROFILE|all");
        }
        return _store.Read(state =>
        {
            IReadOnlyList<UfwApplicationProfile> profiles;
            if (arguments[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                profiles = [.. state.ApplicationProfiles.OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)];
            }
            else
            {
                UfwApplicationProfile profile = state.ApplicationProfiles.FirstOrDefault(profile => profile.Name.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
                    ?? throw Error($"Could not find profile '{arguments[0]}'");
                profiles = [profile];
            }

            for (int index = 0; index < profiles.Count; index++)
            {
                if (index != 0)
                {
                    Console.WriteLine();
                }
                UfwApplicationProfile profile = profiles[index];
                Console.WriteLine("Profile: " + profile.Name);
                Console.WriteLine("Title: " + profile.Title);
                Console.WriteLine("Description: " + profile.Description);
                Console.WriteLine();
                string[] ports = profile.Ports.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Console.WriteLine(ports.Length > 1 || ports[0].Contains(',', StringComparison.Ordinal) ? "Ports:" : "Port:");
                foreach (string port in ports)
                {
                    Console.WriteLine("  " + port);
                }
            }
            return 0;
        });
    });

    public int AppDefault(IReadOnlyList<string> arguments) => Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw Error("Usage: ufw app default allow|deny|reject|skip");
        }
        string policy = arguments[0].ToUpperInvariant() switch
        {
            "ALLOW" => "allow",
            "DENY" => "deny",
            "REJECT" => "reject",
            "SKIP" => "skip",
            _ => throw Error($"Invalid application policy '{arguments[0]}'."),
        };
        _store.Update(options.DryRun, state =>
        {
            state.DefaultApplicationPolicy = policy;
            return 0;
        });
        Console.WriteLine("Default application policy changed to '" + policy + "'");
        return 0;
    });

    public int AppUpdate(bool addNew, IReadOnlyList<string> arguments) => Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw Error("Usage: ufw app update [--add-new] PROFILE|all");
        }
        if (addNew && arguments[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            throw Error("Cannot specify 'all' with '--add-new'");
        }

        List<string> names = _store.Read(state =>
        {
            if (arguments[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return state.ApplicationProfiles.Select(static profile => profile.Name).ToList();
            }
            UfwApplicationProfile profile = state.ApplicationProfiles.FirstOrDefault(profile => profile.Name.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
                ?? throw Error($"Could not find profile '{arguments[0]}'");
            return [profile.Name];
        });

        foreach (string name in names)
        {
            UpdateApplicationProfile(name, addNew);
        }
        return 0;
    });

    private int MutateRule(
        FirewallAction action,
        IReadOnlyList<string> arguments,
        bool routed,
        RulePlacement placement,
        int? insertNumber)
    {
        List<RuleMutationResult> results = _store.Update(options.DryRun, state =>
        {
            ParsedRuleRequest request = _parser.Parse(action, arguments, routed, state);
            IReadOnlyList<UfwMockRule> concreteRules = request.Materialize(state.IPv6Enabled);
            bool spansAddressFamilies = concreteRules.Count > 1;
            InsertPlacementContext? insertContext = placement == RulePlacement.Insert
                ? CreateInsertPlacementContext(state.Rules, insertNumber)
                : null;

            List<RuleMutationResult> mutationResults = [];
            foreach (UfwMockRule rule in concreteRules)
            {
                UfwMockRule? existing = state.Rules.FirstOrDefault(candidate => UfwRuleComparer.SemanticallyEqual(candidate, rule));
                if (existing is not null)
                {
                    if (placement == RulePlacement.Append
                        && !string.Equals(existing.Specification.Comment, rule.Specification.Comment, StringComparison.Ordinal))
                    {
                        existing.Specification.Comment = rule.Specification.Comment;
                        mutationResults.Add(new RuleMutationResult(rule, RuleMutationKind.Updated));
                    }
                    else
                    {
                        mutationResults.Add(new RuleMutationResult(rule, RuleMutationKind.Skipped));
                    }
                    continue;
                }

                InsertRule(state.Rules, rule, placement, insertContext, spansAddressFamilies);
                mutationResults.Add(new RuleMutationResult(rule, placement == RulePlacement.Insert ? RuleMutationKind.Inserted : RuleMutationKind.Added));
            }
            return mutationResults;
        });

        bool enabled = _store.Read(static state => state.Enabled);
        foreach (RuleMutationResult result in results)
        {
            Console.WriteLine(FormatMutationResult(enabled, result));
        }
        return 0;
    }

    private static InsertPlacementContext CreateInsertPlacementContext(List<UfwMockRule> rules, int? insertNumber)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (insertNumber is null || insertNumber <= 0 || insertNumber > rules.Count)
        {
            throw Error($"Invalid position '{insertNumber}'.");
        }

        UfwMockRule target = rules[insertNumber.Value - 1];
        FirewallAddressFamily otherFamily = target.Specification.AddressFamily == FirewallAddressFamily.IPv4
            ? FirewallAddressFamily.IPv6
            : FirewallAddressFamily.IPv4;
        UfwMockRule? counterpart = rules.FirstOrDefault(candidate =>
            candidate.Specification.AddressFamily == otherFamily
            && UfwRuleComparer.SemanticallyEqualIgnoringAddressFamily(candidate, target));

        return target.Specification.AddressFamily == FirewallAddressFamily.IPv4
            ? new InsertPlacementContext(insertNumber.Value, FirewallAddressFamily.IPv4, target, counterpart)
            : new InsertPlacementContext(insertNumber.Value, FirewallAddressFamily.IPv6, counterpart, target);
    }

    private static void InsertRule(
        List<UfwMockRule> rules,
        UfwMockRule rule,
        RulePlacement placement,
        InsertPlacementContext? insertContext,
        bool spansAddressFamilies)
    {
        FirewallAddressFamily family = rule.Specification.AddressFamily;
        int familyStart = family == FirewallAddressFamily.IPv6
            ? rules.FindIndex(static candidate => candidate.Specification.AddressFamily == FirewallAddressFamily.IPv6)
            : 0;
        if (familyStart < 0)
        {
            familyStart = rules.Count;
        }

        int familyCount = rules.Count(candidate => candidate.Specification.AddressFamily == family);
        int index = placement switch
        {
            RulePlacement.Append => familyStart + familyCount,
            RulePlacement.Prepend => familyStart,
            RulePlacement.Insert => ResolveInsertIndex(rules, rule, insertContext, spansAddressFamilies),
            _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, null),
        };
        rules.Insert(index, rule);
    }

    private static int ResolveInsertIndex(
        List<UfwMockRule> rules,
        UfwMockRule rule,
        InsertPlacementContext? context,
        bool spansAddressFamilies)
    {
        if (context is null)
        {
            throw new InvalidOperationException("Insert placement context is required for inserted rules.");
        }

        FirewallAddressFamily family = rule.Specification.AddressFamily;
        UfwMockRule? familyTarget = family switch
        {
            FirewallAddressFamily.IPv4 => context.IPv4Target,
            FirewallAddressFamily.IPv6 => context.IPv6Target,
            _ => throw new InvalidOperationException("Inserted mock rules must have a concrete address family."),
        };

        if (!spansAddressFamilies && context.UserTargetFamily != family)
        {
            throw Error($"Invalid position '{context.UserPosition}'.");
        }

        if (familyTarget is not null)
        {
            int targetIndex = rules.IndexOf(familyTarget);
            if (targetIndex >= 0)
            {
                return targetIndex;
            }
        }

        if (family == FirewallAddressFamily.IPv4)
        {
            int firstIpv6 = rules.FindIndex(static candidate => candidate.Specification.AddressFamily == FirewallAddressFamily.IPv6);
            return firstIpv6 < 0 ? rules.Count : firstIpv6;
        }

        return rules.Count;
    }

    private int DeleteByNumber(int displayNumber)
    {
        if (displayNumber <= 0)
        {
            throw Error("Rule numbers are one-based.");
        }
        if (!options.Force && !Confirm($"Deleting rule {displayNumber}. Proceed with operation (y|n)? "))
        {
#pragma warning disable CA1303 // Fixed English text is part of the UFW-compatible CLI surface.
            Console.WriteLine("Aborted");
#pragma warning restore CA1303
            return 0;
        }

        UfwMockRule removed = _store.Update(options.DryRun, state =>
        {
            if (displayNumber > state.Rules.Count)
            {
                throw Error("Could not delete non-existent rule");
            }
            UfwMockRule rule = state.Rules[displayNumber - 1];
            state.Rules.RemoveAt(displayNumber - 1);
            return rule;
        });
        bool enabled = _store.Read(static state => state.Enabled);
        Console.WriteLine(FormatMutationMessage(enabled, "deleted", removed.Specification.AddressFamily));
        return 0;
    }

    private void UpdateApplicationProfile(string name, bool addNew)
    {
        string policy = _store.Read(static state => state.DefaultApplicationPolicy);
        Console.WriteLine("Rules updated for profile '" + name + "'");
        if (!addNew || policy == "skip")
        {
            return;
        }

        bool exists = _store.Read(state => state.Rules.Any(rule =>
            string.Equals(rule.SourceApplicationName, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.DestinationApplicationName, name, StringComparison.OrdinalIgnoreCase)));
        if (exists)
        {
            return;
        }

        FirewallAction action = policy switch
        {
            "allow" => FirewallAction.Allow,
            "deny" => FirewallAction.Deny,
            "reject" => FirewallAction.Reject,
            _ => throw Error($"Unsupported application policy '{policy}'."),
        };
        _ = MutateRule(action, [name], routed: false, RulePlacement.Append, null);
    }

    private static void RequireNoArguments(IReadOnlyList<string> arguments, string usage)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count != 0)
        {
            throw Error($"Usage: {usage}");
        }
    }

    private static FirewallAction ParseAction(string value) => value.ToUpperInvariant() switch
    {
        "ALLOW" => FirewallAction.Allow,
        "DENY" => FirewallAction.Deny,
        "REJECT" => FirewallAction.Reject,
        "LIMIT" => FirewallAction.Limit,
        _ => throw Error($"Unknown rule action '{value}'."),
    };

    private static string FormatMutationResult(bool enabled, RuleMutationResult result)
    {
        string suffix = result.Rule.Specification.AddressFamily == FirewallAddressFamily.IPv6 ? " (v6)" : string.Empty;
        return result.Kind switch
        {
            RuleMutationKind.Skipped => "Skipping adding existing rule" + suffix,
            RuleMutationKind.Added => FormatMutationMessage(enabled, "added", result.Rule.Specification.AddressFamily),
            RuleMutationKind.Inserted => enabled ? "Rule inserted" + suffix : "Rules updated" + suffix,
            RuleMutationKind.Updated => FormatMutationMessage(enabled, "updated", result.Rule.Specification.AddressFamily),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Kind, null),
        };
    }

    private static string FormatMutationMessage(bool enabled, string operation, FirewallAddressFamily family)
    {
        string suffix = family == FirewallAddressFamily.IPv6 ? " (v6)" : string.Empty;
        return enabled ? $"Rule {operation}{suffix}" : "Rules updated" + suffix;
    }

    private static bool Confirm(string prompt)
    {
        Console.Write(prompt);
        string? answer = Console.ReadLine();
        return answer?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static int Execute(Func<int> operation)
    {
        try
        {
            return operation();
        }
        catch (UfwCliException exception)
        {
            Console.Error.WriteLine("ERROR: " + exception.Message);
            return 1;
        }
    }

    private static UfwCliException Error(string message) => new(message);

    private enum RulePlacement
    {
        Append,
        Insert,
        Prepend,
    }

    private enum RuleMutationKind
    {
        Added,
        Inserted,
        Updated,
        Skipped,
    }

    private sealed record RuleMutationResult(UfwMockRule Rule, RuleMutationKind Kind);

    private sealed record InsertPlacementContext(
        int UserPosition,
        FirewallAddressFamily UserTargetFamily,
        UfwMockRule? IPv4Target,
        UfwMockRule? IPv6Target);
}

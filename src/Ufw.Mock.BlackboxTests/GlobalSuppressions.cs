// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Maintainability",
    "CA1515:Consider making public types internal",
    Justification = "These types are used for testing purposes and need to be public for discovery by the test runner.",
    Scope = "namespaceanddescendants",
    Target = "~N:Ufw.Mock.BlackboxTests")]

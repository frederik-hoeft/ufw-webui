using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Small immutable expected-value arrays keep unit-test assertions local and readable.",
    Scope = "module")]

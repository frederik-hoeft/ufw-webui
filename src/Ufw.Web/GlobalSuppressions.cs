// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Style",
    "IDE0161:Convert to file-scoped namespace",
    Justification = "Auto-generated EF Core migration files",
    Scope = "namespaceanddescendants",
    Target = "~N:Ufw.Web.Data.Migrations")]

[assembly: SuppressMessage(
    "Maintainability",
    "CA1515:Consider making public types internal",
    Justification = "The WKG model-discovery source generator emits these public support types into the consuming assembly.",
    Scope = "namespaceanddescendants",
    Target = "~N:Wkg.EntityFrameworkCore.Discovery.SourceGeneration")]

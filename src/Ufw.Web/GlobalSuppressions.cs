// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress IDE0161 (file-scoped namespace) for auto-generated EF Core migration files
[assembly: SuppressMessage
(
    "Style",
    "IDE0161:Convert to file-scoped namespace",
    Justification = "Auto-generated EF Core migration files",
    Scope = "namespaceanddescendants",
    Target = "~N:UfwWebUI.Data.Migrations"
)]
[assembly: SuppressMessage
(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Razor PageModel classes require instance members",
    Scope = "namespaceanddescendants", Target = "~N:Ufw.Web.Pages"
)]

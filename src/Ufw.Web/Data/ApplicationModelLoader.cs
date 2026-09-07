using Wkg.EntityFrameworkCore.Configuration;
using Wkg.EntityFrameworkCore.Discovery.SourceGeneration;

namespace Ufw.Web.Data;

/// <summary>
/// Source-generated loader for Entity Framework model configurations owned by Ufw.Web.
/// </summary>
[ModelLoader(AssemblyDiscoveryFailureBehavior = AssemblyDiscoveryFailureBehavior.Error, TargetAssemblies = ["Ufw.Web"])]
internal sealed partial class ApplicationModelLoader;

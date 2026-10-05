namespace Ufw.Web.Data.Access;

/// <summary>
/// Represents a typed mutation failure. Payload-bearing derived records preserve domain context where a categorical enum would be insufficient.
/// </summary>
public abstract record DataMutationError;

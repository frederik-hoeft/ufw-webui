namespace Ufw.Web.Client.Features.Rules;

public sealed record RulePositionChange(int OriginalPosition, int CurrentPosition, bool DirectlyMoved);

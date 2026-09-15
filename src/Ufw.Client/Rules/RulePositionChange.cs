namespace Ufw.Client.Rules;

internal sealed record RulePositionChange(int OriginalPosition, int CurrentPosition, bool DirectlyMoved);

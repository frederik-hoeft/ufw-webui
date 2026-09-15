namespace Ufw.Client.Rules;

public sealed record RulePositionChange(int OriginalPosition, int CurrentPosition, bool DirectlyMoved);

namespace Ufw.Roslyn.Controllers.Routing;

internal static class WildcardUtil
{
    public static bool Matches(ReadOnlySpan<char> input, ReadOnlySpan<char> pattern)
    {
        int patternIndex = 0;
        int inputIndex = 0;
        int lastWildcardPatternIndex = -1;
        int lastWildcardInputIndex = -1;
        
        while (inputIndex < input.Length)
        {
            char inputChar = input[inputIndex];
            if (patternIndex < pattern.Length)
            {
                char patternChar = pattern[patternIndex];
                if (patternChar == '*')
                {
                    lastWildcardPatternIndex = patternIndex;
                    lastWildcardInputIndex = inputIndex;
                    ++patternIndex;
                    continue;
                }
                if (inputChar == patternChar)
                {
                    ++patternIndex;
                    ++inputIndex;
                    continue;
                }
            }
            if (lastWildcardPatternIndex == -1)
            {
                return false;
            }

            patternIndex = lastWildcardPatternIndex + 1;
            inputIndex = lastWildcardInputIndex + 1;
            ++lastWildcardInputIndex;
        }
        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            ++patternIndex;
        }

        return patternIndex == pattern.Length;
    }
}

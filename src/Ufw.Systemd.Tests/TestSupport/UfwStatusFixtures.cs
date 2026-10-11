namespace Ufw.Systemd.Tests.TestSupport;

internal static class UfwStatusFixtures
{
    public const string EMPTY_ACTIVE = """
        Status: active

             To                         Action      From
             --                         ------      ----
        """;

    public const string INACTIVE = """
        Status: inactive
        """;

    public const string TWO_RULES = """
        Status: active

             To                         Action      From
             --                         ------      ----
        [ 1] 22/tcp                     ALLOW IN    Anywhere                   # ssh
        [ 2] 80/tcp                     ALLOW IN    192.168.1.0/24
        """;

    public const string IPV6_RULE = """
        Status: active

             To                         Action      From
             --                         ------      ----
        [ 1] 22/tcp (v6)                ALLOW IN    Anywhere (v6)              # ssh
        """;

    public const string DUPLICATE_RULES = """
        Status: active

             To                         Action      From
             --                         ------      ----
        [ 1] 22/tcp                     ALLOW IN    Anywhere
        [ 2] 22/tcp                     ALLOW IN    Anywhere
        """;

    public static string WithRules(params string[] rows)
    {
        return "Status: active\n\n     To                         Action      From\n     --                         ------      ----\n"
            + string.Join('\n', rows)
            + "\n";
    }
    public static string VerboseFromNumbered(string numbered)
    {
        if (numbered.Contains("Status: inactive", StringComparison.Ordinal))
        {
            return "Status: inactive";
        }
        string[] lines = numbered.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimStart();
            if (line.StartsWith('[') && line.IndexOf(']') is int boundary && boundary > 0)
            {
                lines[index] = line[(boundary + 1)..].TrimStart();
            }
        }
        string text = string.Join('\n', lines);
        return text.Replace("Status: active", "Status: active\nLogging: on (low)\nDefault: deny (incoming), allow (outgoing), deny (routed)\nNew profiles: skip", StringComparison.Ordinal);
    }
}

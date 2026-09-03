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
        [ 4] 22/tcp (v6)                ALLOW IN    Anywhere (v6)              # ssh
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
}

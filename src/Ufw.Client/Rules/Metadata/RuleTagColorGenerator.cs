using System.Globalization;
using System.Security.Cryptography;

namespace Ufw.Client.Rules.Metadata;

internal sealed class RuleTagColorGenerator : IRuleTagColorGenerator
{
    private const double SATURATION = 0.72;
    private const double LIGHTNESS = 0.56;

    public string Generate() => FromHue(RandomNumberGenerator.GetInt32(360));

    internal static string FromHue(double hue)
    {
        double normalizedHue = ((hue % 360) + 360) % 360 / 360;
        double chroma = (1 - Math.Abs((2 * LIGHTNESS) - 1)) * SATURATION;
        double segment = normalizedHue * 6;
        double x = chroma * (1 - Math.Abs((segment % 2) - 1));
        (double red, double green, double blue) = (int)Math.Floor(segment) switch
        {
            0 => (chroma, x, 0d),
            1 => (x, chroma, 0d),
            2 => (0d, chroma, x),
            3 => (0d, x, chroma),
            4 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        double match = LIGHTNESS - (chroma / 2);
        int r = ToByte(red + match);
        int g = ToByte(green + match);
        int b = ToByte(blue + match);
        return string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
    }

    private static int ToByte(double channel) => (int)Math.Round(channel * 255, MidpointRounding.AwayFromZero);
}

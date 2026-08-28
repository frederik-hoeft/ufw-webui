namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// ISO 3309 / ITU-T V.42 CRC-32 (polynomial 0xEDB88320, reflected).
/// </summary>
internal static class ItpCrc32
{
    private const uint POLYNOMIAL = 0xEDB88320u;
    private const uint INITIAL = 0xFFFFFFFFu;
    private const uint FINAL_XOR = 0xFFFFFFFFu;

    private static readonly uint[] s_table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data) => AppendFinalized(INITIAL ^ FINAL_XOR, data);

    public static uint Append(uint finalizedCrc, ReadOnlySpan<byte> data) => AppendFinalized(finalizedCrc, data);

    private static uint AppendFinalized(uint finalizedCrc, ReadOnlySpan<byte> data)
    {
        uint crc = finalizedCrc ^ FINAL_XOR;
        for (int i = 0; i < data.Length; i++)
        {
            crc = s_table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ FINAL_XOR;
    }

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1u) != 0 ? (crc >> 1) ^ POLYNOMIAL : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }
}

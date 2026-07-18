using System.Globalization;

namespace MedicalAppointments.Application.Common;

internal static class ConcurrencyToken
{
    public static string ToToken(uint version) => version.ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(string token, out uint version) =>
        uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out version);
}

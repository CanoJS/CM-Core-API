using System.Globalization;

namespace MedicalAppointments.Application.Specialties;

internal static class SpecialtyVersion
{
    public static string ToToken(uint version) => version.ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(string token, out uint version) =>
        uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out version);
}

using System.Globalization;

namespace Gaode.Application.Recipes;

public static class RecipeStageIdentity
{
    public static string Create(int number) => number > 0
        ? "stage:" + number.ToString(CultureInfo.InvariantCulture)
        : throw new ArgumentOutOfRangeException(nameof(number));

    public static bool TryParse(string? id, out int number)
    {
        number = 0;
        return id is not null && id.StartsWith("stage:", StringComparison.Ordinal) &&
            int.TryParse(id.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
            number > 0 && StringComparer.Ordinal.Equals(id, Create(number));
    }
}

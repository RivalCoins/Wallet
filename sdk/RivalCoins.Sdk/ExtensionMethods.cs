using stellar_dotnet_sdk;

namespace RivalCoins.Sdk;

public static class ExtensionMethods
{
    public static string Code(this Asset asset)
    {
        return asset.CanonicalName().Split(':')[0];
    }

    public static string Issuer(this Asset asset)
    {
        return asset.CanonicalName().Split(':')[1];
    }

    public static string ToStellarQuantityString(this double quantity)
        => quantity.ToString($"N{Constants.StellarQuantityMaxDecimalPlaces}");

    public static long ToStroops(this double quantity)
        => long.Parse(quantity.ToStellarQuantityString().Replace(".", string.Empty));

    public static long ToStroops(this string quantity)
        => long.Parse(double.Parse(quantity).ToString($"F{Constants.StellarQuantityMaxDecimalPlaces}").Replace(".", string.Empty));
}
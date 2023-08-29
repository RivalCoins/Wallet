using FsCheck;

namespace VanityCoinGenerator.Tests.Generators;

public static class BooleanGenerator
{
    public static Arbitrary<bool> Generate() => Arb.Default.Bool();
}

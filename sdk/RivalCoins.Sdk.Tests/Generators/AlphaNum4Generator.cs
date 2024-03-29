﻿using Fare;
using FsCheck;

namespace VanityCoinGenerator.Tests.Generators;

public static class AlphaNum4Generator
{
    public static Arbitrary<string> Generate()
    {
        var regexGenerator = new Xeger("([a-zA-Z0-9]){1,4}");

        return Gen.Sized(assetCodeLength => Gen.Elements(Enumerable.Range(1, assetCodeLength).Select(_ => regexGenerator.Generate()))).ToArbitrary();
    }
}

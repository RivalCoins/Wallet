using NUnit.Framework;

namespace RivalCoins.Airdrop.Test.Common.Generic;

[TestFixture]
public class TestClassBase<TSystemUnderTest> : TestClassBase where TSystemUnderTest : class
{
    #region Properties

    /// <summary>
    ///     The system under test
    /// </summary>
    protected TSystemUnderTest SUT { get; set; }

    #endregion Properties

    #region Tear Down

    protected override void OnTearDown()
    {
        base.OnTearDown();

        if (this.SUT is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (this.SUT is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    #endregion Tear Down
}
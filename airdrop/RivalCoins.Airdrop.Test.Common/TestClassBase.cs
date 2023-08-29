using NUnit.Framework;

namespace RivalCoins.Airdrop.Test.Common;

[TestFixture]
public class TestClassBase
{
    #region Setup

    [SetUp]
    public void Setup()
    {
        this.OnSetup();
    }

    protected virtual void OnSetup()
    {
    }

    #endregion Setup

    #region Tear Down

    [TearDown]
    public void TearDown()
    {
        this.OnTearDown();
    }

    protected virtual void OnTearDown()
    {
    }

    #endregion Tear Down
}
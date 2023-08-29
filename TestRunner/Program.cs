static double GetMoneyPrice(long currencyTargetMarketPopulation, long currencySupply)
{
    const long HumanPopulation = 8L * 1000L * 1000L * 1000L;
    const long MoneySupply = 9600 * 1000L * 1000L * 1000L;

    var currencyTargetMarketPercentageOfWorld = currencyTargetMarketPopulation / (double)HumanPopulation;
    var moneyAllocationToCurrencyTargetMarket = currencyTargetMarketPercentageOfWorld * MoneySupply;

    return moneyAllocationToCurrencyTargetMarket / currencySupply;
}
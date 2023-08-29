using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RivalCoins.Airdrop.Common.Config;

public interface IAirdropConfig
{
    string AirdropAccountSeed { get; }
}

public class AirdropConfig : IAirdropConfig
{
    public string AirdropAccountSeed { get; set; }
}
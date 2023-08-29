using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RivalCoins.Airdrop.Common.Config;

public interface IPinwheelConfig
{
    string ApiKey { get; }
    string EnvironmentUrl { get; }
}

public class PinwheelConfig : IPinwheelConfig
{
    public string ApiKey { get; set; }
    public string EnvironmentUrl { get; set; }
}
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RivalCoins.Sdk;

public record StellarTransaction(bool Success, string? L1TransactionXdr, string? L2TransactionXdr, string Message);
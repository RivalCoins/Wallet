using Pulumi;
using Pulumi.Kubernetes.Storage.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Provider = Pulumi.Kubernetes.Provider;

namespace RivalCoins.Infrastructure.Stack.App;

public abstract class EphemeralStackBase : DigitalOceanAppStack
{
    protected EphemeralStackBase(StorageClass persistentStorageClass, Provider provider)
    : base(persistentStorageClass, provider)
    {
    }

    protected override InputList<VolumeMountArgs>? HorizonVolumeMountArgs(Input<string> volumeName) => null;
}

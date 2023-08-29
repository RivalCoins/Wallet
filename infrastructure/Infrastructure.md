# Build
API Server (execute in root directory)
```
docker build -t registry.digitalocean.com/rivalcoins/api:dev -f .\Server\RivalCoins.Server\Dockerfile .
```

# Local Development
Create local 3-node Kubernetes cluster (execute in 'infrastructure\RivalCoins.Infrastructure' directory with 'PowerShell 7' or 'PowerShell Core')
```
$minikubeProfie="profile5"
minikube -p $minikubeProfie start -n 3 --memory 4096 --cni=kindnet --extra-config=kubeadm.pod-network-cidr=10.244.0.0/16 --mount-string="$env:USERPROFILE\.aspnet\https:/https" --mount
minikube -p $minikubeProfie addons enable volumesnapshots
minikube -p $minikubeProfie addons enable csi-hostpath-driver
minikube -p $minikubeProfie image load registry.digitalocean.com/rivalcoins/stellar-quickstart:dev
minikube -p $minikubeProfie image load mock-company-site:dev
minikube -p $minikubeProfie image load registry.digitalocean.com/rivalcoins/api:dev
minikube -p $minikubeProfie image load wallet:dev
minikube -p $minikubeProfie tunnel &
pulumi import -y --skip-preview kubernetes:storage.k8s.io/v1:StorageClass csi-hostpath-sc csi-hostpath-sc
pulumi import -y --skip-preview kubernetes:storage.k8s.io/v1:StorageClass standard standard
```

Prepare Helm
```
helm repo add hashicorp https://helm.releases.hashicorp.com
helm repo update
```

Deploy
```
pulumi stack select <My Local Development Stack Name>
pulumi up
```

# Digital Ocean
Set Deployment Environment
```
$Env:DOTNET_ENVIRONMENT = <"Test" | "Production">
```

Set the Digital Ocean token
```
$env:DIGITALOCEAN_TOKEN=$(read-host -maskinput)
```

Login to Digital Ocean
```
doctl auth init
```

Deploy (first pass)
```
pulumi stack select <Test | Production>
pulumi up
```

Set Kubernetes config
```
pulumi stack output KubeConfig > $env:USERPROFILE\kubeconfig.yml
$env:KUBECONFIG="$env:USERPROFILE\kubeconfig.yml"
```

Import Existing Resources
```
pulumi import -y --skip-preview kubernetes:storage.k8s.io/v1:StorageClass do-block-storage do-block-storage
```

Deploy (second pass)
```
pulumi stack select <Test | Production>
pulumi up
```

HTTPS Access (execute in 'infrastructure\RivalCoins.Infrastructure' directory)
```
helm repo add jetstack https://charts.jetstack.io
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx 
helm repo update
helm install cert-manager --version v1.12.3 jetstack/cert-manager --set installCRDs=true

helm install ingress-nginx ingress-nginx/ingress-nginx --set installCRDs=true --set controller.publishService.enabled=true --set service.beta.kubernetes.io/do-loadbalancer-enable-proxy-protocol=false --set service.beta.kubernetes.io/do-loadbalancer-tls-passthrough=false
helm install -f .\ingress-nginx-values.yaml ingress-nginx ingress-nginx/ingress-nginx

kubectl apply -f .\ClusterIssuer.yaml
kubectl apply -f .\Ingress.yaml
```

# Bootstrap
1. Initialize Stellar (stop command via CTRL+C after all services start up)
```kubectl exec -it horizon-0 -- /start --standalone```
```kubectl delete pod horizon-0```
1. Create immutable supply of "Fake USA" via ```RivalCoins.Bootstrap``` project
1. Initialize Vault (Execute in PowerShell Core and replace secrets)
```
kubectl exec -it vault-0 -n vault -- /bin/sh

vault operator login

vault auth enable kubernetes
vault secrets enable -path=secret kv-v2

vault write auth/kubernetes/config \
  token_reviewer_jwt="$(cat /var/run/secrets/kubernetes.io/serviceaccount/token)" \
  kubernetes_host="https://$KUBERNETES_PORT_443_TCP_ADDR:443" \
  kubernetes_ca_cert=@/var/run/secrets/kubernetes.io/serviceaccount/ca.crt        

vault kv put secret/rivalcoins/test/fake-usa \
  issuer_seed="<CHANGE ME>" \
  distributor_seed="<CHANGE ME>" \
  wrapper_issuer_seed="<CHANGE ME>" \
  wrapper_distributor_seed="<CHANGE ME>"

vault kv put secret/rivalcoins/prod/fake-usa \
  issuer_seed="<CHANGE ME>" \
  distributor_seed="<CHANGE ME>" \
  wrapper_issuer_seed="<CHANGE ME>" \
  wrapper_distributor_seed="<CHANGE ME>"

vault kv put secret/rivalcoins/dev/fake-usa \
  issuer_seed="<CHANGE ME>" \
  distributor_seed="<CHANGE ME>" \
  wrapper_issuer_seed="<CHANGE ME>" \
  wrapper_distributor_seed="<CHANGE ME>"

vault policy write mockcompanysite - <<EOF
path "secret/data/rivalcoins/test/fake-usa" {
  capabilities = ["read"]
}
EOF

vault policy write api-test - <<EOF
path "secret/data/rivalcoins/test/fake-usa" {
  capabilities = ["read"]
}
EOF

vault policy write api-prod - <<EOF
path "secret/data/rivalcoins/prod/fake-usa" {
  capabilities = ["read"]
}
EOF

vault policy write api-dev - <<EOF
path "secret/data/rivalcoins/dev/fake-usa" {
  capabilities = ["read"]
}
EOF

vault write auth/kubernetes/role/busybox \
  policies=mockcompanysite \
  bound_service_account_names=busybox \
  bound_service_account_namespaces=default

vault write auth/kubernetes/role/mock-company-site \
  policies=mockcompanysite \
  bound_service_account_names=mock-company-site \
  bound_service_account_namespaces=default

vault write auth/kubernetes/role/api-test \
  policies=api-test \
  bound_service_account_names=api \
  bound_service_account_namespaces=test

vault write auth/kubernetes/role/api-prod \
  policies=api-prod \
  bound_service_account_names=api \
  bound_service_account_namespaces=prod

vault write auth/kubernetes/role/api-dev \
  policies=api-dev \
  bound_service_account_names=api \
  bound_service_account_namespaces=dev

exit
```

Delete the terminal history because it recorded the secrets
```kubectl delete pod vault-0 -n vault -- /bin/sh```

1. Update stellar.toml
1. Update Rival Coins WordPress plugin (```usa_wrapper_handler```, ```trust_asset_handler```, ```refreshStats()```)

# Destroy Environment
Execute in 'infrastructure\RivalCoins.Infrastructure' directory
```
pulumi state unprotect "urn:pulumi:test::RivalCoins.Infrastructure::kubernetes:storage.k8s.io/v1:StorageClass::do-block-storage"
pulumi destroy
```

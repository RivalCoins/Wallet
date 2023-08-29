DATE=$(date '+%Y%m%d')

doctl auth init
read -p "Paste the command to connect to the Kubernetes cluster: " KUBECONFIG
$KUBECONFIG

kubectl exec -it vault-0 -n vault -- /bin/sh -c /var/lib/config/add-vanity-coin-secret.sh

kubectl cp vault-0:/home/vault/$DATE-rivalcoins.snapshot /images/$DATE-rivalcoins.snapshot -n vault

ls /images
kubectl get pods -n mainnet
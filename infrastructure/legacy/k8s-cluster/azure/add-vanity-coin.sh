DATE=$(date '+%Y%m%d')

az login
az aks get-credentials -n RivalCoinsAKSCluster -g rivalcoins

kubectl exec -it vault-0 -n vault -- /bin/sh -c /var/lib/config/add-vanity-coin-secret.sh

kubectl cp vault-0:/home/vault/$DATE-rivalcoins.snapshot /images/$DATE-rivalcoins.snapshot -n vault
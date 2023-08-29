read -p "Did you update LocalVolume-N-PersistentVolume.yaml? <Press ENTER> "

cd /deployment/k8s-cluster/digital-ocean/deployment

doctl auth init
read -p "Paste the command to connect to the Kubernetes cluster: " KUBECONFIG
$KUBECONFIG

# Create namespaces
kubectl create namespace vault
kubectl create namespace testnet
kubectl create namespace mainnet

# Set up persistent storage
kubectl apply -f LocalStorage-StorageClass.yaml -n vault
kubectl apply -f LocalVolume-0-PersistentVolume.yaml -n vault
kubectl apply -f LocalVolume-1-PersistentVolume.yaml -n vault
kubectl apply -f LocalVolume-2-PersistentVolume.yaml -n vault

# Install Vault
kubectl apply -f Vault-ConfigMap.yaml -n vault
helm repo add hashicorp https://helm.releases.hashicorp.com
helm install vault hashicorp/vault --values vault-do-values.yml -n vault

# Initialize Vault
sleep 30
kubectl exec -it vault-0 -n vault -- vault operator init
kubectl exec -it vault-0 -n vault -- vault login

# restore snapshot
ls /images
read -p "What is the file name of the snapshot to restore?: " SNAPSHOT_FILE
kubectl cp /images/$SNAPSHOT_FILE vault-0:/home/vault/$SNAPSHOT_FILE -n vault
kubectl exec -it vault-0 -n vault -- vault operator raft snapshot restore -force /home/vault/$SNAPSHOT_FILE
kubectl exec -ti vault-0 -n vault -- /bin/sh /var/lib/config/update-k8s-config.sh
kubectl exec -ti vault-1 -n vault -- vault operator raft join http://vault-0.vault-internal:8200
kubectl exec -ti vault-2 -n vault -- vault operator raft join http://vault-0.vault-internal:8200

# Integrate container registry
doctl registry kubernetes-manifest | kubectl apply -f - 
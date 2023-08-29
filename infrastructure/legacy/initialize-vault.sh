helm install vault hashicorp/vault --set "server.dev.enabled=true"
kubectl exec -it vault-0 -- /bin/sh

vault operator login

vault auth enable kubernetes
vault secrets enable -path=secret kv-v2

vault write auth/kubernetes/config \
  token_reviewer_jwt="$(cat /var/run/secrets/kubernetes.io/serviceaccount/token)" \
  kubernetes_host="https://$KUBERNETES_PORT_443_TCP_ADDR:443" \
  kubernetes_ca_cert=@/var/run/secrets/kubernetes.io/serviceaccount/ca.crt        

vault kv put secret/rivalcoins/l1/fake-money \
  issuer_seed="" \
  distributor_seed=""

vault kv put secret/rivalcoins/l2/fake-money \
  wrapper_issuer_seed="" \
  wrapper_distributor_seed="" \
  bank_seed=""

vault kv put secret/rivalcoins/l1/fake-usa \
  issuer_seed="" \
  distributor_seed="" \
  wrapper_issuer_seed="" \
  wrapper_distributor_seed=""

vault policy write mockcompanysite - <<EOF
path "secret/data/rivalcoins/l1/fake-money" {
  capabilities = ["read"]
}
path "secret/data/rivalcoins/l2/fake-money" {
  capabilities = ["read"]
}
EOF

vault policy write appserver - <<EOF
path "secret/data/rivalcoins/l1/fake-usa" {
  capabilities = ["read"]
}
path "secret/data/rivalcoins/l1/fake-money" {
  capabilities = ["read"]
}
path "secret/data/rivalcoins/l2/fake-money" {
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

vault write auth/kubernetes/role/api \
  policies=appserver \
  bound_service_account_names=api \
  bound_service_account_namespaces=default

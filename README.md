[![](../../workflows/gh-pages/badge.svg)](../../actions)


This project contains the official wallets for the Rival Coins ecosystem.

Start Local Mock rivalcoins.money
docker run -it --rm -p 8080:8080 -v /home/jerome/MockRivalCoinsSite:/public danjellz/http-server
docker run -it -d -e CORS_REVERSE_PROXY_TARGET_URL=http://172.241.0.1:8080 -e CORS_REVERSE_PROXY_HOST=0.0.0.0 -p 80:8081 --name mock-rivalcoins-website-cors-proxy kaishuu0123/cors-reverse-proxy

Start Local Stellar + Horizon + Friendbot
docker run --rm -it -p "8000:8000" --name stellar stellar/quickstart --standalone

Rival Coins Server
docker run -it -d `
  -e CORS_REVERSE_PROXY_TARGET_URL=http://localhost:5123 `
  -e CORS_REVERSE_PROXY_HOST=0.0.0.0 `
  -p 6123:8081 `
  --name wallet-server-cors-proxy `
  kaishuu0123/cors-reverse-proxy


Horizon => http://localhost:8000
Friendbot => http://localhost:8000/friendbot?addr={YOUR ACCOUNT ID}

Digital Ocean App
Command - /start --standalone --enable-core-artificially-accelerate-time-for-testing

Digital Ocean Droplet
Command - docker run --rm -it -p "8000:8000" -v "/mnt/horizon_l2_dev:/opt/stellar" --name stellar registry.digitalocean.com/rivalcoins/stellar-quickstart:v0.1.5 --standalone --enable-core-artificially-accelerate-time-for-testing

Server Install
Adapted from https://dev.to/ianknighton/hosting-a-net-core-app-with-nginx-and-let-s-encrypt-1m50
# Install CertBot
```
apt install -y certbot
certbot certonly --standalone -d horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money
```
# Install Nginx
```
apt install -y nginx
ufw allow 'Nginx Full'
nano /etc/nginx/sites-available/horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money
```
Put the following contents in the following file
```
server { 
    listen 80;
    server_name horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money;
    return 301 https://$host$request_uri;
}

server {
    listen 443;
    server_name horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money;

    ssl_certificate           /etc/letsencrypt/live/horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money/fullchain.pem;
    ssl_certificate_key       /etc/letsencrypt/live/horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money/privkey.pem;

    ssl on;
    ssl_session_cache  builtin:1000  shared:SSL:10m;
    ssl_protocols  TLSv1 TLSv1.1 TLSv1.2;
    ssl_ciphers HIGH:!aNULL:!eNULL:!EXPORT:!CAMELLIA:!DES:!MD5:!PSK:!RC4;
    ssl_prefer_server_ciphers on;

    gzip  on;
    gzip_http_version 1.1;
    gzip_vary on;
    gzip_comp_level 6;
    gzip_proxied any;
    gzip_types text/plain text/html text/css application/json application/javascript application/x-javascript text/javascript text/xml application/xml application/rss+xml application/atom+xml application/rdf+xml;
    gzip_buffers 16 8k;
    gzip_disable “MSIE [1-6].(?!.*SV1)”;

    access_log  /var/log/nginx/horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money.access.log;

location / {
    proxy_pass            http://localhost:8000;        
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection keep-alive;
    proxy_set_header   Host $host;
    proxy_cache_bypass $http_upgrade;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    } 
}
```
Execute
```
ln -s /etc/nginx/sites-available/horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money /etc/nginx/sites-enabled/horizon-{l1 | l2}-{Your Desired Environment}.rivalcoins.money
systemctl restart nginx
```
# Install Docker
```
apt install -y docker.io
```

# Authenticate with Digital Ocean
```
snap install doctl
snap connect doctl:dot-docker
doctl auth init
doctl registry login
```

# Run Stellar
```
docker run --rm -it -p "8000:8000" -v "/mnt/stellar-rivalcoins-{l1 | l2}-{Your Desired Environment}:/opt/stellar" --name stellar registry.digitalocean.com/rivalcoins/stellar-quickstart:v0.1.5 --standalone --enable-core-artificially-accelerate-time-for-testing
```

# Run Kubernetes locally
Access Kubernetes apps via the host (Run in a dedicated terminal tab)
```
minikube tunnel
```

Load container images into Minikube
```
minikube image load <Name of Container>
```

Generate Visual Studio SSL certificates
```
dotnet dev-certs https -ep $env:USERPROFILE\.aspnet\https\aspnetapp.pfx -p <CREDENTIAL_PLACEHOLDER>
dotnet dev-certs https --trust
```

Mount Visual Studio SSL certificates into Minikube for access by pods
(Run in dedicated terminal tab)
```
minikube mount $env:USERPROFILE\.aspnet\https:/https
```

Set Development Environment
```
$Env:DOTNET_ENVIRONMENT = "Development"
```

# Minikube
minikube -p profile4 start -n 3 --cni=kindnet --extra-config=kubeadm.pod-network-cidr=10.244.0.0/16 --mount-string="$env:USERPROFILE\.aspnet\https:/https" --mount
minikube -p profile4 addons enable volumesnapshots
minikube -p profile4 addons enable csi-hostpath-driver
minikube -p profile4 image load registry.digitalocean.com/rivalcoins/stellar-quickstart:v0.1.5
minikube -p profile4 image load mock-company-site:dev
minikube -p profile4 image load rivalcoins-api:dev
minikube -p profile4 image load wallet:dev
minikube -p profile4 tunnel &
pulumi import -y --skip-preview kubernetes:storage.k8s.io/v1:StorageClass csi-hostpath-sc csi-hostpath-sc

# Build
API
Run from root folder
```
 docker build -t registry.digitalocean.com/rivalcoins/api:dev -f .\Server\RivalCoins.Server\Dockerfile .
```
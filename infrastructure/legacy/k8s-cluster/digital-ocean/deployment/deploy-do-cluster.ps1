docker run -it --rm `
--mount type=bind,source=c:\src\vanitycoindeployment,target=/deployment `
--mount type=bind,source=C:\Users\jerom\OneDrive\RivalCoins\Volumes,target=/images rivalcoins-k8s-cli:v1.0.0 `
/bin/bash -c /deployment/k8s-cluster/digital-ocean/deployment/deploy-do-cluster.sh
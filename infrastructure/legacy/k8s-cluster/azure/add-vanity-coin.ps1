 docker run -it --rm `
  --mount type=bind,source=c:\src\vanitycoindeployment,target=/deployment `
  --mount type=bind,source=C:\Users\jerom\OneDrive\RivalCoins\Volumes,target=/images azure-cli `
  /bin/bash -c /deployment/k8s-cluster/azure/add-vanity-coin.sh
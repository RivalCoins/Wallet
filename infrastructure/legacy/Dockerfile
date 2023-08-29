# This image is for managing Azure via CLI

FROM ubuntu:20.04

# Install Azure CLI
RUN apt update && apt install -y sudo curl && \
    curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Install Kubernetes CLI
RUN curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl" && \
    sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl

# Install Helm
RUN curl https://get.helm.sh/helm-v3.5.4-linux-amd64.tar.gz > helm.tar.gz && \
    tar -xvf helm.tar.gz && \
    install -o root -g root -m 0755 linux-amd64/helm /usr/local/bin/helm && \
    rm -rf linux-amd64 helm.tar.gz

# Install Digital Ocean CLI
RUN curl -sL https://github.com/digitalocean/doctl/releases/download/v1.61.0/doctl-1.61.0-linux-amd64.tar.gz | tar -xzv && \
    mv doctl /usr/local/bin    
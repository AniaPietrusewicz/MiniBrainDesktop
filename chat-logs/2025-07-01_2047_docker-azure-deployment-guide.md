# Docker on Azure Deployment Guide

**Chat Log**: 2025-07-01_2047  
**User**: Ania  
**Query**: How do I deploy Docker on Azure?

## Docker Deployment Options on Azure

There are several ways to deploy Docker containers on Azure, each with different use cases and complexity levels:

### 1. Azure Container Instances (ACI) - Simplest Option
**Best for**: Quick deployment, testing, simple apps without orchestration needs

**Steps**:
```powershell
# Install Azure CLI if not already installed
winget install Microsoft.AzureCLI

# Login to Azure
az login

# Create resource group
az group create --name myResourceGroup --location eastus

# Deploy container directly
az container create `
    --resource-group myResourceGroup `
    --name mycontainer `
    --image myregistry.azurecr.io/myapp:latest `
    --cpu 1 `
    --memory 1 `
    --registry-login-server myregistry.azurecr.io `
    --registry-username <username> `
    --registry-password <password> `
    --dns-name-label myapp-dns `
    --ports 80
```

### 2. Azure Container Apps - Serverless Containers
**Best for**: Microservices, event-driven apps, auto-scaling needs

**Steps**:
```powershell
# Install Container Apps extension
az extension add --name containerapp

# Create Container Apps environment
az containerapp env create `
    --name myContainerAppEnv `
    --resource-group myResourceGroup `
    --location eastus

# Deploy container app
az containerapp create `
    --name mycontainerapp `
    --resource-group myResourceGroup `
    --environment myContainerAppEnv `
    --image myregistry.azurecr.io/myapp:latest `
    --target-port 80 `
    --ingress external `
    --min-replicas 0 `
    --max-replicas 10
```

### 3. Azure Kubernetes Service (AKS) - Full Orchestration
**Best for**: Complex multi-container apps, production workloads, need for Kubernetes features

**Steps**:
```powershell
# Create AKS cluster
az aks create `
    --resource-group myResourceGroup `
    --name myAKSCluster `
    --node-count 2 `
    --enable-addons monitoring `
    --generate-ssh-keys

# Get credentials
az aks get-credentials --resource-group myResourceGroup --name myAKSCluster

# Deploy using kubectl
kubectl create deployment myapp --image=myregistry.azurecr.io/myapp:latest
kubectl expose deployment myapp --type=LoadBalancer --port=80
```

### 4. Azure App Service for Containers
**Best for**: Web applications, existing App Service users, need for staging slots

**Steps**:
```powershell
# Create App Service plan
az appservice plan create `
    --name myAppServicePlan `
    --resource-group myResourceGroup `
    --is-linux `
    --sku B1

# Create web app with container
az webapp create `
    --resource-group myResourceGroup `
    --plan myAppServicePlan `
    --name mywebapp `
    --deployment-container-image-name myregistry.azurecr.io/myapp:latest
```

### 5. Azure Virtual Machines with Docker
**Best for**: Full control, existing VM infrastructure, hybrid scenarios

**Steps**:
```powershell
# Create VM with Docker pre-installed
az vm create `
    --resource-group myResourceGroup `
    --name myDockerVM `
    --image UbuntuLTS `
    --admin-username azureuser `
    --generate-ssh-keys `
    --custom-data cloud-init.txt

# cloud-init.txt content:
# #cloud-config
# package_upgrade: true
# packages:
#   - docker.io
# runcmd:
#   - systemctl start docker
#   - systemctl enable docker
#   - usermod -aG docker azureuser
```

## Azure Container Registry (ACR) Setup
**Essential for storing your Docker images securely**

```powershell
# Create container registry
az acr create `
    --resource-group myResourceGroup `
    --name myregistry `
    --sku Basic `
    --admin-enabled true

# Login to registry
az acr login --name myregistry

# Build and push image
docker build -t myapp .
docker tag myapp myregistry.azurecr.io/myapp:latest
docker push myregistry.azurecr.io/myapp:latest
```

## Cost Comparison (Rough Estimates)

1. **ACI**: $0.0012/vCPU/second + $0.00013/GB/second
2. **Container Apps**: $0.000024/vCPU/second + $0.000004/GB/second (with consumption plan)
3. **AKS**: ~$0.10/hour/node + compute costs
4. **App Service**: $13.14/month (B1) + additional features
5. **VM**: $29.67/month (B2s) + storage

## Recommendations by Use Case

- **Quick demo/testing**: Azure Container Instances
- **Production web app**: Azure Container Apps or App Service
- **Microservices architecture**: Azure Container Apps or AKS
- **Complex orchestration needs**: AKS
- **Need full VM control**: Azure VMs with Docker
- **Hybrid/on-premises integration**: Azure Arc-enabled Kubernetes

## Security Best Practices

1. Use Azure Container Registry for private images
2. Enable managed identity where possible
3. Use Azure Key Vault for secrets
4. Enable container scanning in ACR
5. Use network policies and private endpoints
6. Implement proper RBAC

## Monitoring & Logging

- Enable Azure Monitor for containers
- Use Application Insights for app telemetry
- Configure log forwarding to Azure Log Analytics
- Set up alerts and dashboards

**Conclusion**: For most scenarios, I'd recommend starting with Azure Container Apps due to its simplicity, serverless nature, and good balance of features vs complexity.

---
**Response completed at**: 2025-07-01_2047

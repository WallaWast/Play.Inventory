# Play.Inventory    
Play Economy Inventory microservice

## Create and publish package
```powershell
$version="1.0.5"
$owner="WallaWast"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Inventory.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/play.inventory -o ..\packages

dotnet nuget push ..\packages\Play.Inventory.Contracts.$version.nupkg --api-key $gh_pat --source "github"
```

## Build the docker image
```powershell
$env:GH_OWNER="WallaWast"
$env:GH_PAT="[PAT HERE]"
$appname="waplayeconomy"
$resourcegroup="playeconomy"
docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$appname.azurecr.io/play.inventory:$version" .
```

## Run the docker image
```powershell
$cosmosDbConnString="[CONN HERE]"
$serviceBusConnString="[CONN STRING HERE]"
docker run -it --rm -p 5004:5004 --name inventory -e MongoDbSettings__ConnectionString=$cosmosDbConnString -e ServiceBusSettings__ConnectionString=$serviceBusConnString -e ServiceSettings__MessageBroker="SERVICEBUS" -e ServiceSettings__Authority=http://localhost:5002 play.inventory:$version
```

## Publishing the docker image
```powershell
az acr login --name $appname
docker push "$appname.azurecr.io/play.inventory:$version"
```

## Creating the Azure Managed Identity and granting it access to Key Vault secrets
```powershell
$namespace="inventory"
az identity create --resource-group $resourcegroup --name $namespace
$IDENTITY_CLIENT_ID=az identity show -g $resourcegroup -n $namespace --query clientId -otsv

az keyvault set-policy -n $appname --secret-permissions get list --spn $IDENTITY_CLIENT_ID
```

## Estabilish the federated identity credential
```powershell
$AKS_OIDC_ISSUER=az aks show -n $appname -g $resourcegroup --query "oidcIssuerProfile.issuerUrl" -otsv

az identity federated-credential create --name $namespace --identity-name $namespace --resource-group $resourcegroup --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount"
```

## Create the Kubernetes namespace
```powershell
kubectl create namespace $namespace
```

## Create the Kubernetes pod
```powershell
kubectl apply -f .\kubernetes\inventory.yaml -n $namespace
```

To check the pods running
```powershell
kubectl get pods -n $namespace
```

## Check the service IP and infos
```powershell
kubectl get services -n $namespace
```
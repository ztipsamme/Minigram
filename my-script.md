## 1. Skapa Infrastruktur

```bash
    RESOURCE_GROUP=""
    APP_PLAN_NAME="plan-minigram"
    WEB_APP_NAME="minigram-api-maritiman"
    VNET_NAME="vnet-minigram"

    # App Service plan (Linux, billigaste tier räcker: B1 eller F1 om tillgängligt)
    az appservice plan create \
        --name plan-minigram \
        --resource-group $RESOURCE_GROUP \
        --sku B1 \
        --is-linux

    # Web App
    az webapp create --name mingram-api-<dittnamn> --resource-group $RESOURCE_GROUP \
    --plan $APP_PLAN_NAME --runtime "DOTNETCORE:8.0"

    # Deploy från lokal build (kör i mappen med .csproj)
    az webapp up --name $WEB_APP_NAME --resource-group $RESOURCE_GROUP
```

## 2. Sätt upp VNet med subnät

### 1. Börja med frontend

```bash
az network vnet create \
    --name $VNET_NAME --resource-group $RESOURCE_GROUP \
    --address-prefix 10.0.0.0/16 \
    --subnet-name frontend-subnet --subnet-prefix 10.0.1.0/24

az network vnet subnet create \
    --name backend-subnet --resource-group $RESOURCE_GROUP \
    --vnet-name $VNET_NAME --address-prefix 10.0.2.0/24

az network nsg create --name nsg-frontend --resource-group $RESOURCE_GROUP
```

### 2. Sätt upp regler

```bash
# Tillåt HTTPS in från internet
az network nsg rule create \
    --nsg-name nsg-frontend --resource-group $RESOURCE_GROUP \
    --name Allow-HTTPS-In --priority 100 \
    --direction Inbound --access Allow --protocol Tcp \
    --source-address-prefixes Internet --destination-port-ranges 443

# Blockera HTTP explicit (lägre prioritetsnummer = körs innan default-regeln, men vi vill vara explicita)
az network nsg rule create \
    --nsg-name nsg-frontend --resource-group $RESOURCE_GROUP \
    --name Deny-HTTP-In --priority 110 \
    --direction Inbound --access Deny --protocol Tcp \
    --source-address-prefixes Internet --destination-port-ranges 80

# Tillåt trafik mellan subnets (frontend <-> backend)
az network nsg rule create \
    --nsg-name nsg-frontend --resource-group $RESOURCE_GROUP \
    --name Allow-Backend-VNet --priority 120 \
    --direction Inbound --access Allow --protocol '*' \
    --source-address-prefixes 10.0.2.0/24 --destination-port-ranges '*'

# Koppla NSG till subnet
az network vnet subnet update \
    --name frontend-subnet --resource-group $RESOURCE_GROUP \
    --vnet-name $VNET_NAME --network-security-group nsg-frontend
```

### 3. Skapa subnät för backend och sätt upp regler

```bash
az network nsg create --name nsg-backend --resource-group $RESOURCE_GROUP

# Tillåt bara trafik från frontend-subnet
az network nsg rule create \
    --nsg-name nsg-backend --resource-group $RESOURCE_GROUP \
    --name Allow-Frontend-VNet --priority 100 \
    --direction Inbound --access Allow --protocol '*' \
    --source-address-prefixes 10.0.1.0/24 --destination-port-ranges '*'

# Koppla NSG till subnet
az network vnet subnet update \
    --name backend-subnet --resource-group $RESOURCE_GROUP \
    --vnet-name $VNET_NAME --network-security-group nsg-backend
```

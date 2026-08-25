## 1. Skapa Infrastruktur

```bash
    $ az appservice plan create \
        -n plan-minigram \
        -g RG-Emma-Spitz-a59389-DotNetCloudDeveloper-VT-Mars-Goteborg \
        --sku B1 \
        --is-linux
```

## 2. Sätt upp VNet med subnät

### 1. Börja med frontend

```bash
az network vnet create \
    --name vnet-mingram --resource-group rg-mingram \
    --address-prefix 10.0.0.0/16 \
    --subnet-name frontend-subnet --subnet-prefix 10.0.1.0/24

az network vnet subnet create \
    --name backend-subnet --resource-group rg-mingram \
    --vnet-name vnet-mingram --address-prefix 10.0.2.0/24

az network nsg create --name nsg-frontend --resource-group rg-mingram
```

### 2. Sätt upp regler

```bash
# Tillåt HTTPS in från internet
az network nsg rule create \
    --nsg-name nsg-frontend --resource-group rg-mingram \
    --name Allow-HTTPS-In --priority 100 \
    --direction Inbound --access Allow --protocol Tcp \
    --source-address-prefixes Internet --destination-port-ranges 443

# Blockera HTTP explicit (lägre prioritetsnummer = körs innan default-regeln, men vi vill vara explicita)
az network nsg rule create \
    --nsg-name nsg-frontend --resource-group rg-mingram \
    --name Deny-HTTP-In --priority 110 \
    --direction Inbound --access Deny --protocol Tcp \
    --source-address-prefixes Internet --destination-port-ranges 80

# Tillåt trafik mellan subnets (frontend <-> backend)
az network nsg rule create \
    --nsg-name nsg-frontend --resource-group rg-mingram \
    --name Allow-Backend-VNet --priority 120 \
    --direction Inbound --access Allow --protocol '*' \
    --source-address-prefixes 10.0.2.0/24 --destination-port-ranges '*'

# Koppla NSG till subnet
az network vnet subnet update \
    --name frontend-subnet --resource-group rg-mingram \
    --vnet-name vnet-mingram --network-security-group nsg-frontend
```

### 3. Skapa subnät för backend och sätt upp regler

```bash
az network nsg create --name nsg-backend --resource-group rg-mingram

# Tillåt bara trafik från frontend-subnet
az network nsg rule create \
    --nsg-name nsg-backend --resource-group rg-mingram \
    --name Allow-Frontend-VNet --priority 100 \
    --direction Inbound --access Allow --protocol '*' \
    --source-address-prefixes 10.0.1.0/24 --destination-port-ranges '*'

# Koppla NSG till subnet
az network vnet subnet update \
    --name backend-subnet --resource-group rg-mingram \
    --vnet-name vnet-mingram --network-security-group nsg-backend
```

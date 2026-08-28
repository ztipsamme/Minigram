STORAGE_NAME="stmingrammaritiman2"
STORAGE_CONTAINER="bilder"
RESOURCE_GROUP="RG-Oskar-Kotlinski-fbed43-DotNetCloudDeveloper-VT-Mars-Goteborg"
VNET_NAME="minigram-vnet"
APP_SERVICE_API_NAME="mingram-api-maritiman-se"

az storage account create \
  --name $STORAGE_NAME \
  --resource-group $RESOURCE_GROUP \
  --location swedencentral \
  --sku Standard_LRS \
  --allow-blob-public-access false

az storage container create \
  --name $STORAGE_NAME \
  --account-name $STORAGE_NAME \
  --auth-mode login

# Tillåt trafik från subnätet via service endpoint
az storage account network-rule add \
  --resource-group $RESOURCE_GROUP \
  --account-name $STORAGE_NAME \
  --subnet "backend-subnet" \
  --vnet-name $VNET_NAME

az storage account update \
  --name $STORAGE_NAME --resource-group $RESOURCE_GROUP \
  --default-action Deny


# Tillåt min IP

MY_IP=$(curl -s https://api.ipify.org)

az storage account network-rule add \
  --resource-group "$RESOURCE_GROUP" \
  --account-name "$STORAGE_NAME" \
  --ip-address "$MY_IP"

# Skapa SAS-token
EXPIRY=$(date -u -v+24H '+%Y-%m-%dT%H:%MZ')

SAS=$(az storage container generate-sas \
  --account-name "$STORAGE_NAME" \
  --name "$STORAGE_CONTAINER" \
  --permissions rl \
  --expiry "$EXPIRY" \
  --auth-mode login \
  --as-user \
  --output tsv)

# Bygg den riktiga URL:en
export BLOB_SAS_URL="https://$STORAGE_NAME.blob.core.windows.net/$STORAGE_CONTAINER?$SAS"


# Lägg den i PROD. Uppdaterar sas-token i azure environment variables
az webapp config appsettings set \
--resource-group "$RESOURCE_GROUP" \
--name $APP_SERVICE_API_NAME \
--settings BLOB_SAS_URL="$BLOB_SAS_URL"

# TA BORT min IP igen
az storage account network-rule remove \
  --resource-group "$RESOURCE_GROUP" \
  --account-name "$STORAGE_NAME" \
  --ip-address "$MY_IP"

# Starta om API:t
az webapp restart \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_API_NAME"

# Ta bort SAS-token i lokala minnet
unset BLOB_SAS_URL



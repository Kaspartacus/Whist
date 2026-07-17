# CosmosBackup

Eksporterer udvalgte production Cosmos DB-containere som komprimerede JSON-filer og uploader dem til en privat Azure Blob Storage-container.

Tool'et kan også kopiere production upload-billeder til samme backup storage account, hvis upload-konfigurationen er sat.

Alle connection strings læses fra environment variables. Connection strings må ikke committes.

## Påkrævede environment variables

```text
COSMOS_CONNECTION_STRING
COSMOS_DATABASE_NAME
BACKUP_STORAGE_CONNECTION_STRING
BACKUP_CONTAINER_NAME
```

## Valgfrie environment variables

```text
BACKUP_PREFIX
BACKUP_CONTAINERS
BACKUP_INCLUDE_REFRESH_TOKENS
UPLOAD_STORAGE_CONNECTION_STRING
UPLOAD_CONTAINER_NAME
UPLOAD_BACKUP_PREFIX
```

Default `BACKUP_PREFIX` er `cosmos-prod`.

Default Cosmos-containere:

```text
users
identityRoles
fines
highlights
counters
points
calendar
rules
```

`refreshTokens` er udeladt som standard, fordi brugere kan logge ind igen efter en restore. Sæt `BACKUP_INCLUDE_REFRESH_TOKENS=true`, hvis session tokens også skal eksporteres.

Hver Cosmos backup uploades som:

```text
cosmos-prod/yyyy-MM-dd/HHmmss-utc/{container}.json.gz
cosmos-prod/yyyy-MM-dd/HHmmss-utc/manifest.json
```

Hvis upload-backup er slået til, kopieres billeder til:

```text
uploads/cosmos-prod/yyyy-MM-dd/HHmmss-utc/{original-blob-name}
uploads/cosmos-prod/yyyy-MM-dd/HHmmss-utc/manifest.json
```

Hvis billeder skal gendannes, skal de kopieres tilbage til production upload-containeren med samme oprindelige blob-sti. Fjern kun backup-prefixet `uploads/cosmos-prod/yyyy-MM-dd/HHmmss-utc/`. Billeder skal ikke pakkes ud med `gunzip`.

I GitHub workflowet skal upload-backup pege på Key Vault secret-navnet og upload-containeren via repository variables:

```text
UPLOAD_STORAGE_CONNECTION_SECRET_NAME
UPLOAD_CONTAINER_NAME
```

For production er de forventede værdier:

```text
UPLOAD_STORAGE_CONNECTION_SECRET_NAME=blobstorage-connectionstring
UPLOAD_CONTAINER_NAME=images
```

For at kigge i en downloadet Cosmos backup-fil på macOS, tjek først om den stadig er gzip-komprimeret:

```bash
file <container>.json.gz
```

Hvis den siger `gzip compressed data`, så pak den ud:

```bash
gunzip -c <container>.json.gz > <container>.json
```

Hvis den siger `JSON data`, har browseren eller Azure Portal allerede pakket indholdet ud, men beholdt filnavnet `.json.gz`. I så fald kan filen kopieres eller omdøbes:

```bash
cp <container>.json.gz <container>.json
```

# CosmosBackup

Exports selected production Cosmos DB containers as compressed JSON files and uploads them to a private Azure Blob Storage container.

The tool reads all configuration from environment variables. Do not commit connection strings.

## Required environment variables

```text
COSMOS_CONNECTION_STRING
COSMOS_DATABASE_NAME
BACKUP_STORAGE_CONNECTION_STRING
BACKUP_CONTAINER_NAME
```

## Optional environment variables

```text
BACKUP_PREFIX
BACKUP_CONTAINERS
BACKUP_INCLUDE_REFRESH_TOKENS
```

Default `BACKUP_PREFIX` is `cosmos-prod`.

Default containers:

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

`refreshTokens` is excluded by default because users can log in again after a restore. Set `BACKUP_INCLUDE_REFRESH_TOKENS=true` if session tokens should also be exported.

Each backup is uploaded as:

```text
cosmos-prod/yyyy-MM-dd/HHmmss-utc/{container}.json.gz
cosmos-prod/yyyy-MM-dd/HHmmss-utc/manifest.json
```

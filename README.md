# Whist

Whist er en lille webapplikation lavet til en privat Whist-gruppe.

Appen hjælper med at samle de ting, der hører til vores spilaftener: medlemmer, point, bøder, highlights, regler og kalenderaftaler.

## Formål

Projektet er et hobbyprojekt, men er bygget som en rigtig full-stack applikation med fokus på enkel struktur, sikker login og mulighed for deployment i Azure.

Målet er ikke at lave et stort enterprise-system, men en overskuelig og vedligeholdbar app, der kan bruges af en lille gruppe brugere.

## Overordnet funktionalitet

- Login og brugerhåndtering
- Medlemsoversigt
- Point og spilskema
- Bøder
- Highlights
- Regler
- Kalender

Nogle data kan ses uden login, mens handlinger der opretter, ændrer eller sletter data kræver login.

## Teknologi

Projektet består af:

- Blazor WebAssembly frontend
- ASP.NET Core Web API backend
- Azure Cosmos DB som database
- JWT-baseret authentication
- Dockeriseret backend
- GitHub Actions til build og deployment

## Arkitektur

Applikationen er bygget som en modular monolith med separat frontend, backend og shared core-projekt.

Backend er stateless og kan køre som container. Frontend er en statisk Blazor WebAssembly-app.

## Deployment

Projektet er forberedt til en enkel Azure deployment med:

- Azure Static Web Apps til frontend
- Azure Container Apps til backend
- Azure Cosmos DB til data
- Azure Key Vault til secrets

Konfiguration og secrets skal håndteres via miljøvariabler og Azure services, ikke hardcodes i source code.

## Lokal development

Lokal development koerer WebApp og ServerAPI paa maskinen, mens data og uploads peger paa separate development-ressourcer i Azure. Production-data maa ikke bruges til lokal test.

Anbefalet development setup:

- Resource group: `rg-whist-dev`
- Cosmos DB for NoSQL: serverless development account
- Cosmos database: `whist-dev`
- Blob Storage container: `images-dev`
- WebApp development API URL: `http://localhost:5176/`

ServerAPI bruger `dotnet user-secrets` til lokale secrets. Secrets ligger udenfor repoet og maa ikke skrives i `appsettings*.json`.

Paakraevede lokale user-secrets for `ServerAPI`:

```text
ConnectionStrings:Database
Database:Name
Database:ThroughputMode
BlobStorage:ConnectionString
BlobStorage:ContainerName
BlobStorage:PublicBaseUrl
Jwt:Issuer
Jwt:Audience
Jwt:Key
Authorization:AdminEmails:0
Smtp:Host
Smtp:Port
Smtp:User
Smtp:Password
Smtp:From
Reminders:RunSecret
DevelopmentSeed:AdminEmail
DevelopmentSeed:AdminPassword
```

`Database:ThroughputMode` skal saettes til `Serverless`, naar den lokale API bruger Cosmos DB serverless. Uden denne vaerdi bruger API'en shared throughput, som production/free tier kan bruge.

Ved foerste lokale startup opretter API'en en development admin-bruger, hvis miljoeet er `Development`, `users` containeren er tom, og `DevelopmentSeed:AdminEmail` samt `DevelopmentSeed:AdminPassword` er sat.

Start lokalt:

```bash
dotnet run --project ServerAPI --launch-profile http
dotnet run --project WebApp --launch-profile http
```

Frontend koerer herefter paa `http://localhost:5102`, og backend koerer paa `http://localhost:5176`.

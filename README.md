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


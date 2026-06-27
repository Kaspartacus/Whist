# Whist App

Dette projekt er en webapplikation lavet til vores Whist-gruppe.  
Vi er fire venner som mødes og spiller kort, og appen hjælper os med at holde styr på vores spil og aktiviteter.

Applikationen bruges til at registrere bøder, holde styr på point, gemme highlights fra spil og planlægge kommende spilledage.

## Funktioner

- Login
- Kalender til planlægning af spilledage
- Registrering af bøder
- Pointsystem (sol-skema)
- Highlights fra spil
- Regelside
- Brugerhåndtering

## Teknologi

Frontend:
- Blazor WebAssembly

Backend:
- ASP.NET Core Web API

Database:
- Azure Cosmos DB for NoSQL

## Login

Alle kan se data i systemet, men kun loggede brugere kan:

- oprette
- redigere
- slette

Login valideres i backend med ASP.NET Core Identity. Beskyttede API-kald
kræver et kortlivet JWT access token. JWT'et indeholder brugerens Identity
security stamp, så logout, password reset og slettede brugere invaliderer
gamle tokens server-side.

Den lokale JWT-signeringsnøgle må ikke gemmes i source control. Opret den med:

```bash
dotnet user-secrets set "Jwt:Key" "<mindst 32 tilfældige tegn>" --project ServerAPI
```

Brugere skal have et Identity `PasswordHash` i databasen. Gamle plaintext
adgangskoder bruges ikke længere af applikationen.

## Formål

Projektet er et hobbyprojekt til vores Whist-aftener og bruges samtidig som øvelse i at arbejde med:

- Blazor
- Web API
- Azure Cosmos DB for NoSQL
- full-stack udvikling

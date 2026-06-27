# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
USER app

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["ServerAPI/ServerAPI.csproj", "ServerAPI/"]
COPY ["Core/Core.csproj", "Core/"]
RUN dotnet restore "ServerAPI/ServerAPI.csproj"

COPY ["ServerAPI/", "ServerAPI/"]
COPY ["Core/", "Core/"]
RUN dotnet publish "ServerAPI/ServerAPI.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM runtime AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServerAPI.dll"]

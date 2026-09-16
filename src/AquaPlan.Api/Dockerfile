FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore AquaPlan.slnx
RUN dotnet publish src/AquaPlan.Api/AquaPlan.Api.csproj -c Release -a $TARGETARCH -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# Le port d'ecoute ne doit pas etre code en dur : Render (et les PaaS en general)
# l'injectent via PORT puis scannent 0.0.0.0:$PORT, et un port fixe fait echouer
# le deploiement avec "no open ports detected".
# Ordre de priorite : ASPNETCORE_URLS explicite, puis $PORT, puis 8080 — cette
# derniere valeur etant celle que supposent les healthchecks docker-compose et NAS.
ENTRYPOINT ["sh", "-c", "exec dotnet AquaPlan.Api.dll --urls \"${ASPNETCORE_URLS:-http://0.0.0.0:${PORT:-8080}}\""]

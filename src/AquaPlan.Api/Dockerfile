FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore AquaPlan.slnx
RUN dotnet publish src/AquaPlan.Api/AquaPlan.Api.csproj -c Release -a $TARGETARCH -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# Npgsql tente une negociation GSSAPI a chaque ouverture de connexion. L'image
# alpine ne fournit pas libgssapi_krb5, ce qui fait echouer le chargement et
# imprime une ligne « Error: ... » au demarrage. La connexion aboutit quand meme
# par repli, mais sur une application ou un message d'erreur trompeur a deja
# coute plusieurs cycles de diagnostic, du bruit d'erreur au boot n'est pas
# acceptable : il masque les vraies pannes.
RUN apk add --no-cache krb5-libs
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# Le port d'ecoute ne doit pas etre code en dur : Render (et les PaaS en general)
# l'injectent via PORT puis scannent 0.0.0.0:$PORT, et un port fixe fait echouer
# le deploiement avec "no open ports detected".
# Ordre de priorite : ASPNETCORE_URLS explicite, puis $PORT, puis 8080 — cette
# derniere valeur etant celle que supposent les healthchecks docker-compose et NAS.
ENTRYPOINT ["sh", "-c", "exec dotnet AquaPlan.Api.dll --urls \"${ASPNETCORE_URLS:-http://0.0.0.0:${PORT:-8080}}\""]

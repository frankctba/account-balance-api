# Build stage runs on the host architecture and cross-compiles for the target,
# so building an amd64 image on Apple Silicon needs no emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY src/AccountApi.Core/AccountApi.Core.csproj src/AccountApi.Core/
COPY src/AccountApi.Api/AccountApi.Api.csproj src/AccountApi.Api/
RUN dotnet restore src/AccountApi.Api -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/AccountApi.Api -a $TARGETARCH -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AccountApi.Api.dll"]

# Stage 1: Base (not used directly)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY DockerUaServer/DockerUaServer.csproj ./DockerUaServer/
COPY DockerUaServer/ZscalerRootCA.pem /usr/local/share/ca-certificates/ZscalerRootCA.crt
RUN update-ca-certificates
RUN openssl s_client -connect api.nuget.org:443 -CAfile /etc/ssl/certs/ca-certificates.crt
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0
RUN dotnet restore "./DockerUaServer/DockerUaServer.csproj"

# Copy source code
COPY ./DockerUaServer ./DockerUaServer/

# Publish the app
RUN dotnet publish ./DockerUaServer/DockerUaServer.csproj -c Release -o /app/out

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "DockerUaServer.dll"]

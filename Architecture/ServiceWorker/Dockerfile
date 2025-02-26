FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["Architecture/ServiceWorker/ServiceWorker.csproj", "Architecture/ServiceWorker/"]
RUN dotnet restore "Architecture/ServiceWorker/ServiceWorker.csproj"
COPY . .
WORKDIR "/src/Architecture/ServiceWorker"
RUN dotnet build "ServiceWorker.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "ServiceWorker.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ServiceWorker.dll"]

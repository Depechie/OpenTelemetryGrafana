FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5500

ENV ASPNETCORE_URLS=http://+:5500

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["Architecture/LocationAPI/LocationAPI.csproj", "Architecture/LocationAPI/"]
RUN dotnet restore "Architecture/LocationAPI/LocationAPI.csproj"
COPY . .
WORKDIR "/src/Architecture/LocationAPI"
RUN dotnet build "LocationAPI.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "LocationAPI.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LocationAPI.dll"]

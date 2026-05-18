FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5103

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY InventoryService.Api/InventoryService.Api.csproj InventoryService.Api/
RUN dotnet restore InventoryService.Api/InventoryService.Api.csproj
COPY . .
WORKDIR /src/InventoryService.Api
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5103
ENTRYPOINT ["dotnet", "InventoryService.Api.dll"]

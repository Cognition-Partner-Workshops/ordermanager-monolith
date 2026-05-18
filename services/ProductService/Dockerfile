FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5102

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ProductService.Api/ProductService.Api.csproj ProductService.Api/
RUN dotnet restore ProductService.Api/ProductService.Api.csproj
COPY . .
WORKDIR /src/ProductService.Api
RUN dotnet build ProductService.Api.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish ProductService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5102
ENTRYPOINT ["dotnet", "ProductService.Api.dll"]

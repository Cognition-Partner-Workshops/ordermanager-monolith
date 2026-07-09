# Stage 1: Build Angular client
FROM node:20-alpine AS client-build
WORKDIR /app
COPY client-app/package*.json client-app/
RUN cd client-app && npm ci
COPY client-app/ client-app/
# angular.json outputs to ../src/OrderManager.Api/wwwroot (browser/ subfolder)
RUN cd client-app && npm run build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
WORKDIR /src
COPY OrderManager.sln ./
COPY src/OrderManager.Api/OrderManager.Api.csproj src/OrderManager.Api/
COPY tests/OrderManager.Api.Tests/OrderManager.Api.Tests.csproj tests/OrderManager.Api.Tests/
RUN dotnet restore
COPY . .
COPY --from=client-build /app/src/OrderManager.Api/wwwroot/browser src/OrderManager.Api/wwwroot/
RUN dotnet publish src/OrderManager.Api/OrderManager.Api.csproj -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=api-build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "OrderManager.Api.dll"]

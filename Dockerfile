# Stage 1: Build Angular frontend
FROM node:18-alpine AS frontend-build
WORKDIR /app/client-app
COPY client-app/package*.json ./
RUN npm ci
COPY client-app/ ./
RUN npx ng build --configuration production

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app
COPY OrderManager.sln ./
COPY src/OrderManager.Api/OrderManager.Api.csproj src/OrderManager.Api/
COPY tests/OrderManager.Api.Tests/OrderManager.Api.Tests.csproj tests/OrderManager.Api.Tests/
RUN dotnet restore OrderManager.sln
COPY src/ src/
COPY --from=frontend-build /app/client-app/dist/client-app/browser/ src/OrderManager.Api/wwwroot/
RUN dotnet publish src/OrderManager.Api/OrderManager.Api.csproj -c Release -o /app/publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "OrderManager.Api.dll"]

FROM node:18-alpine AS frontend-build

WORKDIR /app/client-app
COPY client-app/package.json client-app/package-lock.json* ./
RUN npm ci --ignore-scripts || npm install
COPY client-app/ ./
RUN npx ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build

ARG VERSION=0.0.0

WORKDIR /src
COPY src/OrderManager.Api/OrderManager.Api.csproj ./OrderManager.Api/
RUN dotnet restore ./OrderManager.Api/OrderManager.Api.csproj
COPY src/ ./
COPY --from=frontend-build /app/client-app/dist/client-app/browser ./OrderManager.Api/wwwroot/

RUN dotnet publish ./OrderManager.Api/OrderManager.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:Version=${VERSION} \
    /p:AssemblyVersion=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

LABEL org.opencontainers.image.source="https://github.com/Cognition-Partner-Workshops/ordermanager-monolith"
LABEL org.opencontainers.image.description="OrderManager Monolith - .NET 8 + Angular 17"

WORKDIR /app
COPY --from=backend-build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderManager.Api.dll"]

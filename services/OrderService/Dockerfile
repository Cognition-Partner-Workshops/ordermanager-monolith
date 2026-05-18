FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5104

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY OrderService.Api/OrderService.Api.csproj OrderService.Api/
RUN dotnet restore OrderService.Api/OrderService.Api.csproj
COPY OrderService.Api/ OrderService.Api/
WORKDIR /src/OrderService.Api
RUN dotnet build OrderService.Api.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish OrderService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5104
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]

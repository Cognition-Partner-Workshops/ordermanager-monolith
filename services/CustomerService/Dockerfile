FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5101

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY CustomerService.Api/CustomerService.Api.csproj CustomerService.Api/
RUN dotnet restore CustomerService.Api/CustomerService.Api.csproj
COPY CustomerService.Api/ CustomerService.Api/
WORKDIR /src/CustomerService.Api
RUN dotnet build CustomerService.Api.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish CustomerService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5101
ENTRYPOINT ["dotnet", "CustomerService.Api.dll"]

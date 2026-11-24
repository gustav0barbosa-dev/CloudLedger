# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CloudLedger.sln ./
COPY backend/CloudLedger.Domain/CloudLedger.Domain.csproj backend/CloudLedger.Domain/
COPY backend/CloudLedger.Application/CloudLedger.Application.csproj backend/CloudLedger.Application/
COPY backend/CloudLedger.Infrastructure/CloudLedger.Infrastructure.csproj backend/CloudLedger.Infrastructure/
COPY backend/CloudLedger.Api/CloudLedger.Api.csproj backend/CloudLedger.Api/

RUN dotnet restore backend/CloudLedger.Api/CloudLedger.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/CloudLedger.Api/CloudLedger.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CloudLedger.Api.dll"]
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and all projects
COPY PaymentIntegration.slnx ./
COPY src/PaymentIntegration.Domain/PaymentIntegration.Domain.csproj src/PaymentIntegration.Domain/
COPY src/PaymentIntegration.Application/PaymentIntegration.Application.csproj src/PaymentIntegration.Application/
COPY src/PaymentIntegration.Infrastructure/PaymentIntegration.Infrastructure.csproj src/PaymentIntegration.Infrastructure/
COPY src/PaymentIntegration.API/PaymentIntegration.API.csproj src/PaymentIntegration.API/

# Restore dependencies
RUN dotnet restore src/PaymentIntegration.API/PaymentIntegration.API.csproj

# Copy remaining source code
COPY src/ src/

# Build and publish without --no-restore so required analyzer/target packages are resolved cleanly
RUN dotnet publish src/PaymentIntegration.API/PaymentIntegration.API.csproj \
    -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# In .NET 8+ and 10 container images, the 'app' non-root user (UID 1654) already exists
COPY --from=build --chown=app:app /app/publish .

USER app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "PaymentIntegration.API.dll"]

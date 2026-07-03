# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the API project and its transitive dependencies (Core, Infrastructure). We restore the
# API project directly rather than the whole solution so that adding other projects to the
# solution (e.g. the Backtest console tool) never breaks the container build.
COPY ElectionForecaster.Api/ElectionForecaster.Api.csproj ElectionForecaster.Api/
COPY ElectionForecaster.Core/ElectionForecaster.Core.csproj ElectionForecaster.Core/
COPY ElectionForecaster.Infrastructure/ElectionForecaster.Infrastructure.csproj ElectionForecaster.Infrastructure/

# Restore dependencies
RUN dotnet restore ElectionForecaster.Api/ElectionForecaster.Api.csproj

# Copy everything else
COPY . .

# Build and publish
RUN dotnet publish ElectionForecaster.Api/ElectionForecaster.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create directory for SQLite database
RUN mkdir -p /app/data

COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}

EXPOSE ${PORT:-10000}

ENTRYPOINT ["dotnet", "ElectionForecaster.Api.dll"]

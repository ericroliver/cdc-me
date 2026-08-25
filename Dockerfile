# Multi-stage Dockerfile for CDC Testing Framework API
# Supports multi-architecture builds (linux/amd64, linux/arm64)

ARG DOTNET_VERSION=10.0

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG TARGETARCH
ARG VERSION=1.0.0

WORKDIR /src

RUN apt-get update
RUN apt-get install -y bash nano

COPY openssl.cnf /etc/ssl/openssl.cnf

# Copy project files
COPY ["cdc-api/cdc-api.csproj", "cdc-api/"]
COPY ["cdc-lib/cdc-lib.csproj", "cdc-lib/"]
COPY ["cdc-models/cdc-models.csproj", "cdc-models/"]
COPY ["cdc-cli/cdc-cli.csproj", "cdc-cli/"]
COPY ["cdc-proto/cdc-utility.csproj", "cdc-proto/"]
COPY ["cdc-api.Tests/cdc-api.Tests.csproj", "cdc-api.Tests/"]
COPY ["cdc-cli.Tests/cdc-cli.Tests.csproj", "cdc-cli.Tests/"]
COPY ["cdc-me.sln", "./"]

# Restore dependencies using solution file
RUN dotnet restore "cdc-me.sln" \
    --runtime linux-musl-$(echo $TARGETARCH | sed 's/amd64/x64/; s/arm64/arm64/')

# Copy source code
COPY . .

# Build solution
RUN dotnet build "cdc-me.sln" \
    --configuration Release \
    --no-restore \
    -p:Version=${VERSION}

# Run tests (optional, can be skipped in production builds)
# RUN dotnet test "cdc-me.sln" \
#     --configuration Release \
#     --verbosity minimal

# Publish API application
RUN dotnet publish "cdc-api/cdc-api.csproj" \
    --configuration Release \
    --runtime linux-musl-$(echo $TARGETARCH | sed 's/amd64/x64/; s/arm64/arm64/') \
    --self-contained false \
    --output /app/publish \
    -p:Version=${VERSION}

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime

# Install ICU libraries for globalization support (required by Microsoft.Data.SqlClient)
# and other necessary packages
RUN apk add --no-cache \
    icu-libs \
    icu-data-full \
    ca-certificates \
    tzdata \
    file \
    && update-ca-certificates

# Set environment variable to enable globalization
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build --chown=appuser:appgroup /app/publish .

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Health check - using wget since it's available in alpine
USER root
RUN apk add --no-cache wget
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/ || exit 1

# Set entrypoint
ENTRYPOINT ["dotnet", "cdc-api.dll"]

# Metadata
LABEL org.opencontainers.image.title="CDC Testing Framework API" \
    org.opencontainers.image.description="A .NET Web API for database change validation using SQL Server CDC functionality" \
    org.opencontainers.image.vendor="CDC Testing Framework Team" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.source="https://github.com/your-org/cdc-me" \
    org.opencontainers.image.documentation="https://github.com/your-org/cdc-me/blob/main/README.md"

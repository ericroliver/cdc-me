# Multi-stage Dockerfile for TokenUsageCollector
# Supports multi-architecture builds (linux/amd64, linux/arm64)

ARG DOTNET_VERSION=8.0
ARG ALPINE_VERSION=3.19

# Build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine${ALPINE_VERSION} AS build
ARG TARGETARCH
ARG VERSION=1.0.0

WORKDIR /src

# Copy project files
COPY ["src/TokenUsageCollector/TokenUsageCollector.csproj", "src/TokenUsageCollector/"]
COPY ["src/TokenUsageCollector.Tests/TokenUsageCollector.Tests.csproj", "src/TokenUsageCollector.Tests/"]
COPY ["TokenUsageCollector.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["global.json", "./"]
COPY ["stylecop.json", "./"]

# Restore dependencies using solution file
RUN dotnet restore "TokenUsageCollector.sln" \
    --runtime linux-musl-$(echo $TARGETARCH | sed 's/amd64/x64/; s/arm64/arm64/')

# Copy source code
COPY . .

# Build solution
RUN dotnet build "TokenUsageCollector.sln" \
    --configuration Release \
    --no-restore \
    -p:Version=${VERSION}

# Run tests (optional, can be skipped in production builds)
# RUN dotnet test "TokenUsageCollector.sln" \
#     --configuration Release \
#     --verbosity minimal

# Publish application
RUN dotnet publish "src/TokenUsageCollector/TokenUsageCollector.csproj" \
    --configuration Release \
    --runtime linux-musl-$(echo $TARGETARCH | sed 's/amd64/x64/; s/arm64/arm64/') \
    --self-contained true \
    --output /app/publish \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:Version=${VERSION}

# Runtime stage
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime-deps:${DOTNET_VERSION}-alpine${ALPINE_VERSION} AS runtime

# Install additional packages if needed
RUN apk add --no-cache \
    ca-certificates \
    tzdata \
    && update-ca-certificates

# Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build --chown=appuser:appgroup /app/publish .

# Ensure executable permissions and verify the file exists
RUN chmod +x ./token-usage-collector

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD ./token-usage-collector --help > /dev/null || exit 1

# Debug: List all files in /app to see what was actually copied
RUN ls -la /app/

# Set entrypoint
ENTRYPOINT ["./token-usage-collector"]

# Default command (show help)
CMD ["--help"]

# Metadata
LABEL org.opencontainers.image.title="Tokenado" \
    org.opencontainers.image.description="A .NET console application for collecting and normalizing LLM provider usage data" \
    org.opencontainers.image.vendor="Tokenado Team" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.source="https://github.com/ericroliver/token-usage-collector" \
    org.opencontainers.image.documentation="https://github.com/ericroliver/token-usage-collector/blob/main/README.md"

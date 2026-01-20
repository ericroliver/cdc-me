# Docker Build Optimization Analysis

## Current Build Time: ~20 minutes (16.5 minutes actual build + post-processing)

## Timing Breakdown from Build Log

### Phase 1: Setup & Authentication (~20 seconds)
- Docker buildx setup: ~5s
- Container registry login: ~1s
- Metadata generation: ~1s

### Phase 2: Actual Docker Build (~16.5 minutes)
- **Multi-platform image pulling** (amd64 + arm64): ~15s per architecture = ~30s
- **apt-get update + install**: ~2-3s per architecture = ~6s
- **Dependency restore**: ~1-2 minutes (estimated from log gaps)
- **Build all projects**: ~3-4 minutes (estimated)
- **Publish API**: ~1-2 minutes
- **Building for two architectures in parallel**: This is the main time consumer
- **Cache export**: **259 seconds (4.3 minutes!)** - Line 1510 in log

### Phase 3: Post-Build (~3.5 minutes)
- SBOM generation: ~9s
- Security scanning: Variable
- Artifact uploads: ~1-2s

## Major Bottlenecks Identified

### 1. **Multi-Architecture Builds (BIGGEST IMPACT: 50% time increase)**
Building for both `linux/amd64` and `linux/arm64` essentially doubles the build time since each architecture needs:
- Separate base image pulls
- Separate dependency restores
- Separate compilations
- Separate publishes

**Impact**: 7-8 minutes
**Recommendation**: Consider building only for amd64 if arm64 isn't required, or use matrix builds to parallelize

### 2. **Cache Export Time (4.3 minutes)**
Line 1510 shows: "preparing build cache for export 259.2s"

This is using GitHub Actions cache with `mode=max` which exports all layers.

**Impact**: 4.3 minutes
**Recommendation**: Use `mode=min` or selective caching

### 3. **Inefficient Layer Structure**
```dockerfile
RUN apt-get update
RUN apt-get install -y bash nano
```
- Separate RUN commands create unnecessary layers
- Installing debugging tools (nano) in production builds
- Bash is already included in the base image

**Impact**: Minimal time, but adds layers
**Recommendation**: Combine commands, remove unnecessary packages

### 4. **Building Entire Solution Including Tests**
```dockerfile
RUN dotnet build "cdc-me.sln" \
    --configuration Release \
    --no-restore \
    -p:Version=${VERSION}
```
- Builds ALL projects (api, lib, models, cli, proto, tests)
- Only cdc-api is published and used
- Test projects add compilation time but aren't used in the image

**Impact**: 1-2 minutes
**Recommendation**: Build only required projects

### 5. **No NuGet Cache Mount**
The Dockerfile doesn't use BuildKit cache mounts for NuGet packages.

**Impact**: 1-2 minutes on cache miss
**Recommendation**: Add `--mount=type=cache` for NuGet

### 6. **Suboptimal Layer Ordering**
```dockerfile
COPY . .
```
This copies all source code before the build, which means ANY source change invalidates the entire cache after this point.

**Current order**:
1. Copy project files ✓
2. Restore dependencies ✓
3. Copy ALL source ← Cache breaks here
4. Build
5. Publish

**Impact**: Forces rebuild on every commit
**Recommendation**: Already optimal for project structure, but could separate by project

### 7. **Runtime Stage Inefficiencies**
```dockerfile
USER root
RUN apk add --no-cache wget
USER appuser
```
Switching users multiple times is unnecessary

**Impact**: Minimal (~1-2 seconds)
**Recommendation**: Install all packages before creating user

## Optimization Priority Matrix

| Optimization | Time Saved | Effort | Priority |
|--------------|------------|--------|----------|
| Build single architecture only | 7-8 min | Low | **HIGH** |
| Use cache mode=min | 3-4 min | Low | **HIGH** |
| Build only required projects | 1-2 min | Medium | **MEDIUM** |
| Add NuGet cache mounts | 1-2 min | Low | **MEDIUM** |
| Combine RUN commands | 5-10 sec | Low | **LOW** |
| Remove debug tools | 2-5 sec | Low | **LOW** |
| Optimize user switching | 1-2 sec | Low | **LOW** |

## Recommended Optimization Strategy

### Option 1: Quick Wins (5-10 minutes saved)
1. Build only for amd64 (save 7-8 min)
2. Change cache mode from `max` to `min` (save 3-4 min)
3. Combine RUN commands
4. Remove unnecessary packages

**Total Time Saved: 10-12 minutes → Build time ~8-10 minutes**

### Option 2: Moderate Optimization (7-12 minutes saved)
All from Option 1, plus:
5. Build only cdc-api and dependencies (save 1-2 min)
6. Add NuGet cache mounts (save 1-2 min on cold builds)

**Total Time Saved: 12-14 minutes → Build time ~6-8 minutes**

### Option 3: Maximum Optimization (Keep multi-arch but optimize)
If you need multi-architecture support:
1. Use cache mode `min` (save 3-4 min)
2. Build only required projects (save 1-2 min)
3. Add NuGet cache mounts (save 1-2 min)
4. Optimize layer structure
5. Use GitHub Actions matrix to build architectures in separate jobs in parallel

**Total Time Saved: 5-8 minutes per architecture → Build time ~12-14 minutes**

## Cost-Benefit Analysis

### Current State
- Build time: 20 minutes
- GitHub Actions cost (2-core runner): ~$0.008/minute
- Cost per build: $0.16
- Daily builds (10): $1.60

### With Option 1 Optimizations
- Build time: 8-10 minutes
- Cost per build: $0.064-$0.08
- Daily builds (10): $0.64-$0.80
- **Savings: 60% time, 60% cost**

### With Option 2 Optimizations
- Build time: 6-8 minutes
- Cost per build: $0.048-$0.064
- Daily builds (10): $0.48-$0.64
- **Savings: 70% time, 70% cost**

## Implementation Recommendations

### Immediate Actions (Do Now)
1. Modify GitHub Actions workflow to build only `linux/amd64` (or use matrix strategy)
2. Change cache mode from `max` to `min` in workflow
3. Update Dockerfile to combine RUN commands
4. Remove nano package from build stage

### Short-term Actions (This Sprint)
1. Update Dockerfile to build only required projects
2. Add BuildKit cache mounts for NuGet packages
3. Optimize runtime stage user operations
4. Consider extracting to .dockerignore any large files still being copied

### Long-term Considerations
1. Evaluate if arm64 support is truly needed
2. If multi-arch is required, implement matrix builds in GitHub Actions
3. Consider using a base image with pre-installed dependencies
4. Evaluate using distroless images for even smaller runtime images
5. Implement build caching in a container registry (GitHub Container Registry supports this)

## Additional Notes

- The GitHub Actions cache has size limits (10GB per repository)
- Cache mode `max` exports all layers, which is why it takes 4+ minutes
- Cache mode `min` only exports layers for the final image, much faster
- Building only for one architecture will cut build time nearly in half
- Multi-stage builds are already properly implemented
- The alpine runtime image is a good choice (small and secure)

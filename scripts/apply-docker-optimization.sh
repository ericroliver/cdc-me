# 1. Use optimized Dockerfile
cp Dockerfile Dockerfile.original
cp Dockerfile.optimized Dockerfile

# 2. Edit .github/workflows/docker.yml
# Change line 140 from:
#   cache-to: type=gha,mode=max
# To:
#   cache-to: type=gha,mode=min

# 3. Verify multi-arch is still set (line 133 should have):
#   platforms: linux/amd64,linux/arm64

# 4. Test locally on your Mac
docker build --platform linux/arm64 -t cdc-api:test .

# 5. Commit and push
git add Dockerfile .github/workflows/docker.yml
git commit -m "feat: optimize Docker build time (multi-arch, 30-40% faster)"
git push

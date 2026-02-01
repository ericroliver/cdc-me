# API Tester Dockerfile
FROM node:20-alpine

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm install

# Copy source
COPY tsconfig.json ./
COPY src ./src
COPY tests ./tests

# Build TypeScript
RUN npm run build

# Default to showing help
ENTRYPOINT ["node", "dist/cli.js"]
CMD ["--help"]

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything first (including tests)
COPY . .

# Restore dependencies (solution at /src/PayFlow.sln after COPY . .)
WORKDIR /src
RUN dotnet restore "PayFlow.sln"

# Build
WORKDIR "/src/src/PayFlow.Api"
RUN dotnet build -c Release --no-restore

# Publish
RUN dotnet publish -c Release -o /app/publish --no-build

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Expose ports (HTTP 5000, HTTPS 5001)
EXPOSE 5000
EXPOSE 5001

ENTRYPOINT ["dotnet", "PayFlow.Api.dll"]

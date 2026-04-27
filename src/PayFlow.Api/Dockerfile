# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything (repo root as context)
COPY . .

# Restore dependencies for the API project (NuGet resolves project refs automatically)
RUN dotnet restore "src/PayFlow.Api/PayFlow.Api.csproj"

# Build
RUN dotnet build "src/PayFlow.Api/PayFlow.Api.csproj" -c Release --no-restore

# Publish
RUN dotnet publish "src/PayFlow.Api/PayFlow.Api.csproj" -c Release -o /app/publish --no-build

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
EXPOSE 5001

ENTRYPOINT ["dotnet", "PayFlow.Api.dll"]

# ================================
# Base image for running the app
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# DigitalOcean App Platform expects the app to listen on $PORT
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

# Expose default port for local dev
EXPOSE 8080

# ================================
# Build and publish the app
# ================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["AlSaqr.API/AlSaqr.API.csproj", "AlSaqr.API/"]
COPY ["AlSaqr.Data/AlSaqr.Data.csproj", "AlSaqr.Data/"]
COPY ["AlSaqr.Domain/AlSaqr.Domain.csproj", "AlSaqr.Domain/"]

RUN dotnet restore "AlSaqr.API/AlSaqr.API.csproj"

# Copy the entire solution and build
COPY . .
WORKDIR "/src/AlSaqr.API"
RUN dotnet build "AlSaqr.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ================================
# Publish the build output
# ================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AlSaqr.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ================================
# Final runtime image
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Set non-root user (DigitalOcean runs containers safely, but it's good practice)
RUN adduser --disabled-password --home /app appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .

# DigitalOcean will automatically inject PORT; this uses it
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENTRYPOINT ["dotnet", "AlSaqr.API.dll"]

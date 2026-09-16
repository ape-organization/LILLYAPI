# =========================
# Build
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy csproj and restore for layer caching
COPY ["LilyAPI.csproj", "./"]

RUN dotnet restore "PharmacyAPI.csproj"

# Copy remaining sources
COPY . .

# Publish
RUN dotnet publish "LilyAPI.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# =========================
# Runtime
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

# ASP.NET Core listens on port 8080 inside container
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Run application
ENTRYPOINT ["dotnet", "LilyAPI.dll"]

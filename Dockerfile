# =========================
# Build
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy csproj and restore for layer caching
COPY ["LilyAPI.csproj", "./"]

RUN dotnet restore "LilyAPI.csproj"

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

# Native libraries required by SkiaSharp
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libfontconfig1 \
        libfreetype6 \
        libpng16-16 \
        libjpeg62-turbo \
        libwebp7 \
        libx11-6 \
        libxcb1 \
        libxext6 \
        libxrender1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

# ASP.NET Core listens on port 8080 inside container
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Run application
ENTRYPOINT ["dotnet", "LilyAPI.dll"]
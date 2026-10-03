# ──────────────────────────────────────────────────────────────────────────────
# Stage 1: Build
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# プロジェクトファイルを先にコピーしてリストア層をキャッシュ
COPY OshikatsuApi/OshikatsuApi.csproj OshikatsuApi/
RUN dotnet restore OshikatsuApi/OshikatsuApi.csproj

# ソースをコピーしてパブリッシュ
COPY OshikatsuApi/ OshikatsuApi/
RUN dotnet publish OshikatsuApi/OshikatsuApi.csproj \
      -c Release \
      -o /app/publish \
      --no-restore

# ──────────────────────────────────────────────────────────────────────────────
# Stage 2: Runtime
# ──────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render は PORT 環境変数でリッスンポートを指定する。未設定時は 8080 を使う。
# Program.cs が PORT を読み取って Kestrel の URL を設定するため、
# ここでは ASPNETCORE_URLS は設定しない。
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "OshikatsuApi.dll"]

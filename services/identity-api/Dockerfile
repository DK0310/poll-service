# ── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the csproj first so `restore` is cached until dependencies change.
COPY ["IdentityApi/IdentityApi.csproj", "IdentityApi/"]
RUN dotnet restore "IdentityApi/IdentityApi.csproj"

COPY IdentityApi/ IdentityApi/
RUN dotnet publish "IdentityApi/IdentityApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "IdentityApi.dll"]

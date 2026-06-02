# ── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the csproj first so `restore` is cached until dependencies change.
COPY ["VoteApi/VoteApi.csproj", "VoteApi/"]
RUN dotnet restore "VoteApi/VoteApi.csproj"

COPY VoteApi/ VoteApi/
RUN dotnet publish "VoteApi/VoteApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VoteApi.dll"]

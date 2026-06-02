# ── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the csproj first so `restore` is cached until dependencies change.
COPY ["PollApi/PollApi.csproj", "PollApi/"]
RUN dotnet restore "PollApi/PollApi.csproj"

# Copy the app source (tests are excluded via .dockerignore / not copied).
COPY PollApi/ PollApi/
RUN dotnet publish "PollApi/PollApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PollApi.dll"]

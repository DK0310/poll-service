# build stage: needs the full SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# restore on its own layer so Docker caches it unless the csproj changes
COPY ["VoteApi/VoteApi.csproj", "VoteApi/"]
RUN dotnet restore "VoteApi/VoteApi.csproj"

COPY VoteApi/ VoteApi/
RUN dotnet publish "VoteApi/VoteApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# runtime stage: aspnet image is much smaller (no SDK/compilers shipped)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VoteApi.dll"]

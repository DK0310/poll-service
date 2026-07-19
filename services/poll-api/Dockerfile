# build stage: needs the full SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# restore on its own layer so Docker caches it unless the csproj changes
COPY ["PollApi/PollApi.csproj", "PollApi/"]
RUN dotnet restore "PollApi/PollApi.csproj"

# only the app project is copied (the test project stays out of the image)
COPY PollApi/ PollApi/
RUN dotnet publish "PollApi/PollApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# runtime stage: aspnet image is much smaller (no SDK/compilers shipped)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PollApi.dll"]

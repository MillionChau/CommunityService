FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy csproj and restore
COPY ["src/Community.Domain/Community.Domain.csproj", "src/Community.Domain/"]
COPY ["src/Community.Application/Community.Application.csproj", "src/Community.Application/"]
COPY ["src/Community.Infrastructure/Community.Infrastructure.csproj", "src/Community.Infrastructure/"]
COPY ["src/Community.API/Community.API.csproj", "src/Community.API/"]

RUN dotnet restore "src/Community.API/Community.API.csproj"

# Copy full source and build
COPY . .
WORKDIR "/app/src/Community.API"
RUN dotnet build "Community.API.csproj" -c Release -o /app/build
RUN dotnet publish "Community.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Community.API.dll"]


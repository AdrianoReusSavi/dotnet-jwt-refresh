FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["src/JwtRefresh.Api/JwtRefresh.Api.csproj", "src/JwtRefresh.Api/"]
COPY ["src/JwtRefresh.Domain/JwtRefresh.Domain.csproj", "src/JwtRefresh.Domain/"]
RUN dotnet restore "src/JwtRefresh.Api/JwtRefresh.Api.csproj"
COPY . .
RUN dotnet publish "src/JwtRefresh.Api/JwtRefresh.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "JwtRefresh.Api.dll"]
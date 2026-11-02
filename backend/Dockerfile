# Stage 1 – build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Restore dependencies first so this layer is cached separately
COPY backend.Api.csproj ./
RUN dotnet restore backend.Api.csproj

COPY . ./
RUN dotnet publish backend.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2 – runtime image only (no SDK or source code)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish ./

EXPOSE 8080

# Use Production; secrets must be supplied through environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "backend.Api.dll"]

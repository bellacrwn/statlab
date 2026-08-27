FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY StatisticalTests.vbproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Render's default web-service port. Render supplies PORT at runtime; this
# value also makes the image runnable locally without extra configuration.
EXPOSE 10000
ENV PORT=10000
ENTRYPOINT ["dotnet", "StatLab.dll"]

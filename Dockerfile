# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, as its own layer, so code changes do not re-download packages.
COPY StudyMate/StudyMate.csproj StudyMate/
RUN dotnet restore StudyMate/StudyMate.csproj

COPY StudyMate/ StudyMate/
RUN dotnet publish StudyMate/StudyMate.csproj -c Release -o /app/publish /p:UseAppHost=false

# Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The SQLite file is written at runtime, so its directory must be writable by the
# non-root user the image runs as. This storage is ephemeral: the host reclaims it
# on every restart, and the database is rebuilt from the seed on the next start.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
USER $APP_UID

ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/studymate.db"
ENV ASPNETCORE_ENVIRONMENT=Production

# Render assigns the port through PORT; Program.cs reads it and falls back to 10000.
EXPOSE 10000

ENTRYPOINT ["dotnet", "StudyMate.dll"]

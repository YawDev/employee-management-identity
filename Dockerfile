# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only csproj files first so the restore layer caches across source-only changes
COPY employee.management.identity/employee.management.identity.csproj                               employee.management.identity/
COPY employee.management.identity.core/employee.management.identity.core.csproj                     employee.management.identity.core/
COPY employee.management.identity.infrastructure/employee.management.identity.infrastructure.csproj employee.management.identity.infrastructure/
COPY employee.management.identity.models/employee.management.identity.models.csproj                 employee.management.identity.models/
COPY employee.management.identity.utility/employee.management.identity.utility.csproj               employee.management.identity.utility/
RUN dotnet restore employee.management.identity/employee.management.identity.csproj

COPY . .
RUN dotnet publish employee.management.identity/employee.management.identity.csproj \
      -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "employee.management.identity.dll"]

# Two stages: a build image that has the SDK and a runtime image that does not.
# Context is the repo root (docker build -f src/MessageBus.Operations.Web/Dockerfile .),
# so central files (Directory.*.props, global.json) are visible during restore.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy just what restore needs first, so a pure source edit reuses the restore layer.
COPY global.json Directory.Build.props Directory.Packages.props NuGet.Config* ./
COPY src/MessageBus.Abstractions/*.csproj src/MessageBus.Abstractions/
COPY src/MessageBus.Core/*.csproj src/MessageBus.Core/
COPY src/MessageBus.OpenTelemetry/*.csproj src/MessageBus.OpenTelemetry/
COPY src/MessageBus.Operations/*.csproj src/MessageBus.Operations/
COPY src/MessageBus.Operations.Web/*.csproj src/MessageBus.Operations.Web/
COPY src/MessageBus.Transport.RabbitMq/*.csproj src/MessageBus.Transport.RabbitMq/
COPY src/MessageBus.Transport.AzureServiceBus/*.csproj src/MessageBus.Transport.AzureServiceBus/

RUN dotnet restore src/MessageBus.Operations.Web/MessageBus.Operations.Web.csproj

# Now the sources.
COPY src/MessageBus.Abstractions/ src/MessageBus.Abstractions/
COPY src/MessageBus.Core/ src/MessageBus.Core/
COPY src/MessageBus.OpenTelemetry/ src/MessageBus.OpenTelemetry/
COPY src/MessageBus.Operations/ src/MessageBus.Operations/
COPY src/MessageBus.Operations.Web/ src/MessageBus.Operations.Web/
COPY src/MessageBus.Transport.RabbitMq/ src/MessageBus.Transport.RabbitMq/
COPY src/MessageBus.Transport.AzureServiceBus/ src/MessageBus.Transport.AzureServiceBus/

RUN dotnet publish src/MessageBus.Operations.Web/MessageBus.Operations.Web.csproj \
 --configuration Release \
 --no-restore \
 --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The published image uses the non-root user baked into the ASP.NET Core base image
# so a compromised process cannot write outside /app.
USER $APP_UID

# Kestrel binds to :8080 by default in the 10.0 base image; TLS is expected upstream.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MessageBus.Operations.Web.dll"]

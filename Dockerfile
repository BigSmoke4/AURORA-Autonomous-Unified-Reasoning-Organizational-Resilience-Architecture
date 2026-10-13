# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Aurora.sln .
COPY src/Shared/Aurora.Shared/Aurora.Shared.csproj src/Shared/Aurora.Shared/
COPY src/Modules/Aurora.Modules.Identity/Aurora.Modules.Identity.csproj src/Modules/Aurora.Modules.Identity/
COPY src/Modules/Aurora.Modules.Organizations/Aurora.Modules.Organizations.csproj src/Modules/Aurora.Modules.Organizations/
COPY src/Modules/Aurora.Modules.DigitalTwin/Aurora.Modules.DigitalTwin.csproj src/Modules/Aurora.Modules.DigitalTwin/
COPY src/Modules/Aurora.Modules.Graph/Aurora.Modules.Graph.csproj src/Modules/Aurora.Modules.Graph/
COPY src/Modules/Aurora.Modules.Scenarios/Aurora.Modules.Scenarios.csproj src/Modules/Aurora.Modules.Scenarios/
COPY src/Modules/Aurora.Modules.Simulation/Aurora.Modules.Simulation.csproj src/Modules/Aurora.Modules.Simulation/
COPY src/Modules/Aurora.Modules.Risk/Aurora.Modules.Risk.csproj src/Modules/Aurora.Modules.Risk/
COPY src/Modules/Aurora.Modules.Decisions/Aurora.Modules.Decisions.csproj src/Modules/Aurora.Modules.Decisions/
COPY src/Modules/Aurora.Modules.AI/Aurora.Modules.AI.csproj src/Modules/Aurora.Modules.AI/
COPY src/Modules/Aurora.Modules.Audit/Aurora.Modules.Audit.csproj src/Modules/Aurora.Modules.Audit/
COPY src/Modules/Aurora.Modules.Analysis/Aurora.Modules.Analysis.csproj src/Modules/Aurora.Modules.Analysis/
COPY src/Modules/Aurora.Modules.Forecasting/Aurora.Modules.Forecasting.csproj src/Modules/Aurora.Modules.Forecasting/
COPY src/Modules/Aurora.Modules.Optimization/Aurora.Modules.Optimization.csproj src/Modules/Aurora.Modules.Optimization/
COPY src/Modules/Aurora.Modules.Workforce/Aurora.Modules.Workforce.csproj src/Modules/Aurora.Modules.Workforce/
COPY src/Modules/Aurora.Modules.Finance/Aurora.Modules.Finance.csproj src/Modules/Aurora.Modules.Finance/
COPY src/Modules/Aurora.Modules.Operations/Aurora.Modules.Operations.csproj src/Modules/Aurora.Modules.Operations/
COPY src/Modules/Aurora.Modules.Technology/Aurora.Modules.Technology.csproj src/Modules/Aurora.Modules.Technology/
COPY src/Modules/Aurora.Modules.Customers/Aurora.Modules.Customers.csproj src/Modules/Aurora.Modules.Customers/
COPY src/Modules/Aurora.Modules.Markets/Aurora.Modules.Markets.csproj src/Modules/Aurora.Modules.Markets/
COPY src/Modules/Aurora.Modules.Assets/Aurora.Modules.Assets.csproj src/Modules/Aurora.Modules.Assets/
COPY src/Modules/Aurora.Modules.Contracts/Aurora.Modules.Contracts.csproj src/Modules/Aurora.Modules.Contracts/
COPY src/Modules/Aurora.Modules.Monitoring/Aurora.Modules.Monitoring.csproj src/Modules/Aurora.Modules.Monitoring/
COPY src/Modules/Aurora.Modules.Alerts/Aurora.Modules.Alerts.csproj src/Modules/Aurora.Modules.Alerts/
COPY src/Modules/Aurora.Modules.Reporting/Aurora.Modules.Reporting.csproj src/Modules/Aurora.Modules.Reporting/
COPY src/Modules/Aurora.Modules.SupplyChain/Aurora.Modules.SupplyChain.csproj src/Modules/Aurora.Modules.SupplyChain/
COPY src/Web/Aurora.Web/Aurora.Web.csproj src/Web/Aurora.Web/
COPY tests/Aurora.Tests/Aurora.Tests.csproj tests/Aurora.Tests/

RUN dotnet restore Aurora.sln

COPY . .
RUN dotnet publish src/Web/Aurora.Web/Aurora.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The runtime loads the Kerberos GSSAPI library on the first database connection. The slim base image
# does not ship it, so the process crashes without it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Data Protection keys (cookie and anti-forgery encryption) live in a directory that a volume can keep.
# Without it, every container restart invalidates every signed-in session.
ENV DataProtection__KeyDirectory=/home/app/keys
RUN mkdir -p /home/app/keys && chown app:app /home/app/keys

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# The aspnet base image ships an unprivileged "app" user. Run as it, not root.
USER app

ENTRYPOINT ["dotnet", "Aurora.Web.dll"]

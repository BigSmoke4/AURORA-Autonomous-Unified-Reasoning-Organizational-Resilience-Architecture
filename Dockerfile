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
COPY src/Web/Aurora.Web/Aurora.Web.csproj src/Web/Aurora.Web/
COPY tests/Aurora.Tests/Aurora.Tests.csproj tests/Aurora.Tests/

RUN dotnet restore Aurora.sln

COPY . .
RUN dotnet publish src/Web/Aurora.Web/Aurora.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Aurora.Web.dll"]

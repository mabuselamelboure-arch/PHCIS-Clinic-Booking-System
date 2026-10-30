FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
# Static web asset inputs affect implicit SDK packages, so restore with the complete source tree.
COPY src ./src
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore src/PHCIS.Api/PHCIS.Api.csproj --locked-mode
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish src/PHCIS.Api/PHCIS.Api.csproj -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime
WORKDIR /app
COPY --from=build --chown=app:app /publish .
RUN mkdir /data && chown app:app /app /data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PHCIS.Api.dll"]

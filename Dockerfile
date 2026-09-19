FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SongVoter.csproj packages.lock.json ./
RUN dotnet restore --locked-mode
COPY . .
COPY appsettings.example.json appsettings.json
RUN dotnet publish -c Release --no-restore -o /app/publish

FROM build AS test
RUN dotnet restore tests/SongVoter.Tests --locked-mode && dotnet test tests/SongVoter.Tests --no-restore --filter Category=Unit

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 4200
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Coflnet.SongVoter.dll"]

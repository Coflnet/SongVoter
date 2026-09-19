FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SongVoter.csproj packages.lock.json ./
RUN dotnet restore --locked-mode
COPY . .
COPY appsettings.example.json appsettings.json
RUN dotnet publish -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 4200
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Coflnet.SongVoter.dll"]

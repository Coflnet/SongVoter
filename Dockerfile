#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Container we use for final publish
FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 4200

# Build container
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

# Copy the code into the container
WORKDIR /
COPY ["/Coflnet.SongVoter/Coflnet.SongVoter.csproj", "Coflnet.SongVoter/"]

# NuGet restore
RUN dotnet restore "Coflnet.SongVoter/Coflnet.SongVoter.csproj"
COPY ["/Coflnet.SongVoter/", "Coflnet.SongVoter/"]

# Build the API
WORKDIR "/Coflnet.SongVoter"
RUN dotnet build "Coflnet.SongVoter.csproj" -c Release -o /app/build

# Publish it
FROM build AS publish
RUN dotnet publish "Coflnet.SongVoter.csproj" -c Release -o /app/publish

# Make the final image for publishing
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Coflnet.SongVoter.dll"]

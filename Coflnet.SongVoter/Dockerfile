#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Container we use for final publish
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build container
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build

# Copy the code into the container
WORKDIR /
COPY ["/Coflnet.SongVoter/Coflnet.SongVoter.csproj", "Coflnet.SongVoter/"]

# NuGet restore
RUN dotnet restore "Coflnet.SongVoter/Coflnet.SongVoter.csproj"
COPY ["/Coflnet.SongVoter/", "Coflnet.SongVoter/"]

# Build the API
WORKDIR "Coflnet.SongVoter"
RUN dotnet build "Coflnet.SongVoter.csproj" -c Release -o /app/build

# Publish it
FROM build AS publish
RUN dotnet publish "Coflnet.SongVoter.csproj" -c Release -o /app/publish

# Make the final image for publishing
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Coflnet.SongVoter.dll"]

#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Container we use for final publish
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 4200

# Build container
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

# Copy the code into the container
WORKDIR /
COPY ["SongVoter.csproj", "SongVoter/"]

# NuGet restore
RUN dotnet restore "SongVoter/SongVoter.csproj"
COPY [".", "SongVoter/"]

# Build the API
WORKDIR "/SongVoter"
RUN dotnet build "SongVoter.csproj" -c Release -o /app/build

# Publish it
FROM build AS publish
RUN dotnet publish "SongVoter.csproj" -c Release -o /app/publish

# Make the final image for publishing
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN useradd --uid $(shuf -i 2000-65000 -n 1) app
USER app
ENTRYPOINT ["dotnet", "Coflnet.SongVoter.dll", "--hostBuilder:reloadConfigOnChange=false"]

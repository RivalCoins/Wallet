#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
#COPY ["RivalCoins.MockCompanySite/RivalCoins.MockCompanySite.csproj", "RivalCoins.MockCompanySite/"]
COPY ["RivalCoins.MockCompanySite.csproj", "RivalCoins.MockCompanySite/"]
RUN dotnet restore "RivalCoins.MockCompanySite/RivalCoins.MockCompanySite.csproj"
#COPY . .
#WORKDIR "/src/RivalCoins.MockCompanySite"
WORKDIR "/src/RivalCoins.MockCompanySite"
COPY . .
RUN dotnet build "RivalCoins.MockCompanySite.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RivalCoins.MockCompanySite.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RivalCoins.MockCompanySite.dll"]
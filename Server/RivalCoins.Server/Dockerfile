#FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
#WORKDIR /app
#EXPOSE 80
#EXPOSE 443
#
#FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
#WORKDIR /src
#COPY ["RivalCoins.Server.csproj", "RivalCoins.Server/"]
#RUN dotnet restore "RivalCoins.Server/RivalCoins.Server.csproj"
#WORKDIR "/src/RivalCoins.Server"
#COPY . .
#RUN dotnet build "RivalCoins.Server.csproj" -c Release -o /app/build
#
#FROM build AS publish
#RUN dotnet publish "RivalCoins.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false
#
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "RivalCoins.Server.dll"]

###########################################################################################3

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
#COPY ["RivalCoins.Server.sln", "RivalCoins.Server/"]
#COPY ../../ Wallet/
#RUN dotnet restore "RivalCoins.Server/Server/RivalCoins.Server.sln"
#WORKDIR /src/Wallet
COPY . .
#WORKDIR /src/Server/RivalCoins.Server
RUN dotnet build Server/RivalCoins.Server/RivalCoins.Server.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish Server/RivalCoins.Server/RivalCoins.Server.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RivalCoins.Server.dll"]
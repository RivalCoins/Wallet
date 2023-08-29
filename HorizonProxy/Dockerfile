#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

#FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

#FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
#COPY ["HorizonProxy/HorizonProxy.csproj", "HorizonProxy/"]
#RUN dotnet restore "HorizonProxy/HorizonProxy.csproj"
COPY ["HorizonProxy.csproj", "HorizonProxy/"]
#RUN dotnet restore "HorizonProxy.csproj"
WORKDIR "/src/HorizonProxy"
COPY . .
RUN dotnet build "HorizonProxy.csproj" -c Release -o /app/build
#RUN dotnet build "/src/HorizonProxy.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HorizonProxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

#FROM base AS final
FROM build AS final
WORKDIR /app
COPY --from=publish /app/publish .
#RUN dotnet dev-certs https
ENTRYPOINT ["dotnet", "HorizonProxy.dll"]
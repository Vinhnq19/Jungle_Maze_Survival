FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY GameServer/GameServer.csproj GameServer/
RUN dotnet restore GameServer/GameServer.csproj
COPY GameServer/ GameServer/
COPY Shared/ Shared/
RUN dotnet publish GameServer/GameServer.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 7777/udp
ENTRYPOINT ["dotnet", "GameServer.dll"]

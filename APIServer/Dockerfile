FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app
RUN apt-get update && apt-get install -y git
RUN git clone https://github.com/jwy0805/CryWolfAPI.git

COPY ./appsettings.json ./CryWolfAPI/APIServer/Migrations/appsettings.json

WORKDIR /app/CryWolfAPI/APIServer
RUN dotnet restore 
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

RUN apt-get update
RUN apt-get install -y telnet
RUN apt-get install -y mariadb-client
RUN rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/out .
COPY ./Config ./Config

ENV ENVIRONMENT Container
ENV CERT_PATH /https/aspnetapp.pfx
ENV CERT_PASSWORD Wy76286688
ENV DATA_PATH /app/Common 
ENV MAP_DATA_PATH /app/Common/MapData 
ENV CONFIG_PATH /app/Config/CryWolfAccountConfig.json 
ENV DB_CONNECTION_STRING Server=crywolf-db;Port=3306;Database=CryWolfDb_0v1;Uid=jwy;Pwd=7628;

EXPOSE 80

ENTRYPOINT ["dotnet", "APIServer.dll"]

FROM mcr.microsoft.com/dotnet/sdk:6.0

RUN apt-get update
RUN apt-get install -y git
RUN apt-get install -y telnet
RUN apt-get install -y mariadb-client

ENV ENVIRONMENT Container
ENV CERT_PATH /https/aspnetapp.pfx
ENV CERT_PASSWORD Wy76286688
ENV DATA_PATH /app/Common
ENV MAP_DATA_PATH /app/Common/MapData
ENV CONFIG_PATH /app/Config/CryWolfAccountConfig.json
ENV DB_CONNECTION_STRING Server=crywolf-db;Port=3306;Database=CryWolfDb_0v1;Uid=jwy;Pwd=7628;

EXPOSE 499

WORKDIR /app/APIServer
CMD ["sh", "-c", "dotnet restore && dotnet watch run"]
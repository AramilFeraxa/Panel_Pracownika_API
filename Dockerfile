FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PanelPracownika.sln .
COPY PanelPracownika/PanelPracownika.csproj ./PanelPracownika/

RUN dotnet restore PanelPracownika.sln

COPY PanelPracownika/ ./PanelPracownika/
WORKDIR /src/PanelPracownika
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "PanelPracownika.dll"]

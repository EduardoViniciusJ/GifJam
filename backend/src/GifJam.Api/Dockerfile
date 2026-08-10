FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["src/GifJam.Api/GifJam.Api.csproj", "src/GifJam.Api/"]
RUN dotnet restore "src/GifJam.Api/GifJam.Api.csproj"

COPY . .
RUN dotnet publish "src/GifJam.Api/GifJam.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
RUN apk add --no-cache krb5-libs

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "GifJam.Api.dll"]

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
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080
RUN apk add --no-cache icu-data-full icu-libs krb5-libs tzdata
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID:$APP_UID /home/app/.aspnet

COPY --from=build /app/publish .
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "GifJam.Api.dll"]

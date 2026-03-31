FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY WebApplication1/ ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app/publish .

# Railway: mount volume at /data via dashboard
RUN mkdir -p /data

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/data/crimecode.db"
EXPOSE 8080

# Railway sets PORT at runtime — must use shell form so $PORT is expanded at runtime
CMD ASPNETCORE_URLS="http://+:${PORT:-8080}" dotnet WebApplication1.dll

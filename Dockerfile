# --- build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/FitBook.Admin/FitBook.Admin.csproj src/FitBook.Admin/
RUN dotnet restore src/FitBook.Admin/FitBook.Admin.csproj
COPY src/ src/
RUN dotnet publish src/FitBook.Admin/FitBook.Admin.csproj -c Release -o /app --no-restore

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# SQLite databáze ve svazku /data, aby přežila restart kontejneru
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__DefaultConnection="DataSource=/data/fitbook-admin.db;Cache=Shared" \
    DisableHttpsRedirection=true
RUN mkdir /data && chown app /data
VOLUME /data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "FitBook.Admin.dll"]

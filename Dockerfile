# Server deployment image. For personal desktop use, run the app directly on Windows instead
# (Windows Credential Manager, folder pickers and "open in editor" only work there).
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY . .
RUN dotnet publish src/Chroniq.Api/Chroniq.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /src/frontend/dist ./wwwroot
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    App__BaseDirectory=/data \
    App__DataDirectory=/data/state \
    Security__UseWindowsCredentialManager=false
VOLUME ["/data"]
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Chroniq.Api.dll"]

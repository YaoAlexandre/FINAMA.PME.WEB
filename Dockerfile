# Étape 1 : Compilation du Blazor WebAssembly
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copier les fichiers projets et restaurer
COPY ["Finama.Web/Finama.Web.csproj", "Finama.Web/"]
RUN dotnet restore "Finama.Web/Finama.Web.csproj"

# Copier tout le code et publier
COPY . .
RUN dotnet publish "Finama.Web/Finama.Web.csproj" -c Release -o out

# Étape 2 : Serveur web Nginx pour héberger les fichiers statiques
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Copier les fichiers compilés de Blazor dans le dossier par défaut de Nginx
COPY --from=build-env /app/out/wwwroot .

# Configuration de secours Nginx pour gérer le routage virtuel Blazor (évite la 404 au F5)
RUN sed -i 'x;s/$/try_files $uri $uri\/ \/index.html;/;G' /etc/nginx/conf.d/default.conf

EXPOSE 80
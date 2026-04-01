FROM node:22-alpine AS miniapp-build
WORKDIR /src/LagerthaAssistant.Web
COPY LagerthaAssistant.Web/package*.json ./
RUN npm ci
COPY LagerthaAssistant.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
COPY --from=miniapp-build /src/LagerthaAssistant.Api/wwwroot/miniapp ./LagerthaAssistant.Api/wwwroot/miniapp
RUN dotnet publish LagerthaAssistant.Api/LagerthaAssistant.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
ENTRYPOINT ["./LagerthaAssistant.Api"]

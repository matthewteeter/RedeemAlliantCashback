#syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/runtime:9.0-noble AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["RedeemAlliantCashback.csproj", "."]
RUN dotnet restore "./RedeemAlliantCashback.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "RedeemAlliantCashback.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RedeemAlliantCashback.csproj" -c Release -o /app/publish

FROM base AS final
USER root
WORKDIR /app
COPY --from=publish /app/publish .
##Need to trigger Playwright install thru code to avoid dependency on sdk in final image - see www.meziantou.net/distributing-applications-that-depend-on-microsoft-playwright.htm
RUN ["dotnet", "/app/RedeemAlliantCashback.dll", "install"]
## Using this dependency list vs relying on Playwright's install-deps saves over 500MB in image size (uncompressed)
RUN <<-DEPS
	apt-get update
	apt-get -y install --no-cache libasound2 libx11-xcb1 libxcursor1 libgtk-3-0 libpangocairo-1.0-0 libcairo-gobject2 libgdk-pixbuf-2.0-0 libdbus-glib-1-2
	apt-get clean
	rm -rf /var/lib/apt/lists/*
DEPS
ENTRYPOINT ["dotnet", "RedeemAlliantCashback.dll"]
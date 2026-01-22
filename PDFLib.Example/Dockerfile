FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Items pulled from Documentation
RUN apt-get update && apt-get install -y --no-install-recommends \
        wget zlib1g fontconfig libfreetype6 libx11-6 libxext6 libxrender1 \
        libssl3 xfonts-75dpi xfonts-base curl unzip libnss3 libatk1.0-0t64 \
        libatk-bridge2.0-0 libcups2 libdrm2 libxcomposite1 libxdamage1 \
        libxfixes3 libxrandr2 libgbm1 libasound2t64 libxkbcommon0 \
        libpango-1.0-0 libpangocairo-1.0-0 libxshmfence1 fonts-liberation \
        libfontconfig1 ca-certificates fonts-ipafont-gothic fonts-wqy-zenhei \
        fonts-thai-tlwg fonts-kacst fonts-freefont-ttf fonts-noto-color-emoji \
        fonts-dejavu-core \
        && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PDFLib.Example/PDFLib.Example.csproj", "PDFLib.Example/"]
RUN dotnet restore "PDFLib.Example/PDFLib.Example.csproj"
COPY . .
WORKDIR "/src/PDFLib.Example"

RUN dotnet build "./PDFLib.Example.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release

# Notice the target for linux!
RUN dotnet publish "./PDFLib.Example.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./PDFLib.Example"]

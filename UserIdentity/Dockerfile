FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src

COPY ["Directory.Build.props", "Directory.Packages.props", ".editorconfig", "./"]
COPY ["UserIdentity/UserIdentity.csproj", "UserIdentity/"]
RUN dotnet restore "UserIdentity/UserIdentity.csproj" \
    -a $TARGETARCH \
    /p:Configuration=Release \
    /p:SelfContained=false \
    /p:PublishReadyToRun=true \
    /p:ExcludeEfDesign=true

COPY ["UserIdentity/", "UserIdentity/"]
WORKDIR "/src/UserIdentity"

RUN dotnet publish "UserIdentity.csproj" \
    -a $TARGETARCH \
    --no-restore \
    -o /app/publish \
    /p:ExcludeEfDesign=true \
    /p:SelfContained=false \
    /p:PublishReadyToRun=true \
    /p:DebugType=none \
    /p:SatelliteResourceLanguages=en \
    /p:PublishDocumentationFiles=false \
    /p:PublishReferencesSymbols=false

FROM base AS final

WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["./UserIdentity"]

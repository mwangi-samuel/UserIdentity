FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src

COPY ["UserIdentity/UserIdentity.csproj", "UserIdentity/"]
RUN dotnet restore "UserIdentity/UserIdentity.csproj" \
    -a $TARGETARCH \
    /p:Configuration=Release \
    /p:SelfContained=true \
    /p:PublishSingleFile=true \
    /p:ExcludeEfDesign=true

COPY ["UserIdentity/", "UserIdentity/"]
WORKDIR "/src/UserIdentity"

RUN dotnet publish "UserIdentity.csproj" \
    -a $TARGETARCH \
    --no-restore \
    -o /app/publish \
    /p:ExcludeEfDesign=true \
    /p:SelfContained=true \
    /p:PublishSingleFile=true \
    /p:EnableCompressionInSingleFile=true \
    /p:DebugType=none \
    /p:SatelliteResourceLanguages=en \
    /p:PublishDocumentationFiles=false \
    /p:PublishReferencesSymbols=false

RUN rm -f \
    /app/publish/librdkafka.so \
    /app/publish/centos8-librdkafka.so

FROM base AS stripped
COPY --from=build /app/publish /app/publish
RUN apk add --no-cache binutils \
    && strip --strip-unneeded /app/publish/alpine-librdkafka.so

FROM base AS final
ARG TARGETARCH
RUN apk upgrade --no-cache
RUN if [ "$TARGETARCH" = "arm64" ]; then \
        apk add --no-cache libsasl; \
    fi

WORKDIR /app
COPY --from=stripped /app/publish .
USER $APP_UID
ENTRYPOINT ["./UserIdentity"]

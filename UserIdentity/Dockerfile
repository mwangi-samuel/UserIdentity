FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["UserIdentity/UserIdentity.csproj", "UserIdentity/"]
RUN dotnet restore "UserIdentity/UserIdentity.csproj" -r linux-musl-x64

COPY . .
WORKDIR "/src/UserIdentity"

RUN dotnet publish "UserIdentity.csproj" \
    -c Release \
    -r linux-musl-x64 \
    -o /app/publish \
    /p:SelfContained=true \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=true \
    /p:EnableCompressionInSingleFile=true \
    /p:InvariantGlobalization=true \
    /p:DebugType=none

# librdkafka.redist ships glibc, CentOS 8 and Alpine builds 
# Confluent.Kafka only loads alpine-librdkafka.so on Alpine
RUN rm /app/publish/librdkafka.so /app/publish/centos8-librdkafka.so

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["./UserIdentity"]

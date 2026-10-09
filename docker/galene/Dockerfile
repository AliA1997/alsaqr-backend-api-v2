# Galène SFU for AlSaqr event video (docs/superpowers/plans/2026-10-04-event-video-streaming.md).
# No official image is published, so this builds a pinned release from source.
FROM golang:1.24-alpine AS build
ARG GALENE_VERSION=galene-1.2.1
RUN apk add --no-cache git \
 && git clone --depth 1 --branch "${GALENE_VERSION}" https://github.com/jech/galene.git /src
WORKDIR /src
RUN CGO_ENABLED=0 go build -ldflags="-s -w" -o /out/galene .

FROM alpine:3.21
COPY --from=build /out/galene /usr/local/bin/galene
COPY --from=build /src/static /srv/galene/static
RUN mkdir -p /srv/galene/data /srv/galene/groups
WORKDIR /srv/galene
# 8443: HTTP(S) + WebSocket + admin API. 1194: built-in TURN. Media uses an ephemeral UDP range.
EXPOSE 8443 1194/tcp 1194/udp
ENTRYPOINT ["galene", "-data", "/srv/galene/data", "-groups", "/srv/galene/groups", "-static", "/srv/galene/static"]

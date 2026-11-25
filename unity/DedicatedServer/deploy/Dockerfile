FROM ubuntu:24.04@sha256:562111b88caa79c71391c0c4d3c2114a7d5c35915c20561fd893cb1e07929e6b

RUN apt-get update && apt-get install -y --no-install-recommends \
        ca-certificates libstdc++6 libgcc-s1 libatomic1 libgl1 libx11-6 libxcursor1 libxrandr2 \
        libxinerama1 libxi6 libasound2t64 libpulse0 libgtk-3-0t64 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 10001 --create-home --shell /usr/sbin/nologin dustline

WORKDIR /opt/dustline
COPY --chown=10001:10001 Builds/PR7/LinuxServer/ ./
COPY --chmod=755 deploy/entrypoint.sh /usr/local/bin/dustline-entrypoint
RUN chmod 755 DustlineServer.x86_64

ENV HOME=/home/dustline
USER 10001:10001
EXPOSE 7777/udp
ENTRYPOINT ["/usr/local/bin/dustline-entrypoint"]

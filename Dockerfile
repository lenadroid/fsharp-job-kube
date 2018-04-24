FROM fsharp
COPY . .
RUN mono ./.paket/paket.bootstrapper.exe
RUN mono ./.paket/paket.exe restore
RUN mono .paket/paket.exe install
EXPOSE 3000
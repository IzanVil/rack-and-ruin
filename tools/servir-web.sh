#!/usr/bin/env bash
#
# Sirve la build WebGL en local para probarla antes de publicar.
# Necesario porque los navegadores no cargan WebGL desde file://.
#
#   ./tools/servir-web.sh [puerto]
#
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIR="$RAIZ/Build/WebGL"
PUERTO="${1:-8000}"

if [[ ! -f "$DIR/index.html" ]]; then
    echo "ERROR: no hay build web en $DIR. Ejecuta antes ./tools/build-web.sh" >&2
    exit 1
fi

echo "Sirviendo $DIR"
echo "Abre en el navegador:  http://localhost:$PUERTO/"
echo "Ctrl+C para parar."
cd "$DIR"
exec python3 -m http.server "$PUERTO"

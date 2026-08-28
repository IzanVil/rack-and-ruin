#!/usr/bin/env bash
#
# Compila la versión web (WebGL) de UPTIME y la deja lista para publicar.
#
#   ./tools/build-web.sh
#
# Salida: Build/WebGL/  (index.html + Build/ + carpetas de Unity)
# Publicable tal cual en GitHub Pages, itch.io o cualquier hosting estático.
#
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.0.82f1/Editor/Unity}"
SALIDA="$RAIZ/Build/WebGL"

if [[ ! -x "$UNITY" ]]; then
    echo "ERROR: no encuentro el editor de Unity en: $UNITY" >&2
    echo "       Ajusta la variable UNITY o instala 6000.0.82f1." >&2
    exit 1
fi

if [[ ! -d "$(dirname "$UNITY")/Data/PlaybackEngines/WebGLSupport" ]]; then
    echo "ERROR: falta el módulo WebGL del editor." >&2
    echo "       Instálalo con:" >&2
    echo "         unity install-modules --editor-version 6000.0.82f1 --module webgl --accept-eula -y" >&2
    exit 1
fi

echo "Compilando WebGL (esto tarda varios minutos)…"
"$UNITY" -batchmode -nographics -quit -accept-apiupdate \
    -projectPath "$RAIZ" \
    -executeMethod ServerGame.EditorTools.BuildTools.BuildWeb \
    -buildOutput "$SALIDA" \
    -logFile -

echo
echo "Listo. Build en: $SALIDA"
echo "Pruébalo en local con:  ./tools/servir-web.sh"

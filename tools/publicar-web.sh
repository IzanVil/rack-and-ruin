#!/usr/bin/env bash
#
# Compila la versión web y la publica en GitHub Pages.
#
#   ./tools/publicar-web.sh                  compila y publica
#   ./tools/publicar-web.sh --sin-compilar   publica la build que ya haya en Build/WebGL
#
# La build no se versiona en main. Esto la empuja a una rama huérfana gh-pages que se
# reescribe entera en cada publicación (push --force sobre un único commit), así que ni
# main ni gh-pages acumulan binarios.
#
# Requisito de una sola vez: en el repo, Settings > Pages > Source = "Deploy from a
# branch", rama gh-pages, carpeta / (root).
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIR="$RAIZ/Build/WebGL"
RAMA="gh-pages"

if [[ "${1:-}" != "--sin-compilar" ]]; then
    "$RAIZ/tools/build-web.sh"
fi

if [[ ! -f "$DIR/index.html" ]]; then
    echo "ERROR: no hay build en $DIR. Ejecuta antes ./tools/build-web.sh" >&2
    exit 1
fi

REMOTO="$(git -C "$RAIZ" remote get-url origin)"
SHA="$(git -C "$RAIZ" rev-parse --short HEAD)"
NOMBRE="$(git -C "$RAIZ" config user.name  || echo "publicar-web")"
CORREO="$(git -C "$RAIZ" config user.email || echo "publicar-web@localhost")"

# Si el árbol está sucio, lo publicado no corresponde a ningún commit y el mensaje de
# gh-pages mentiría. Se avisa, pero no se bloquea: a veces se quiere publicar una prueba.
if ! git -C "$RAIZ" diff --quiet HEAD -- Assets ProjectSettings 2>/dev/null; then
    echo "AVISO: hay cambios sin commitear en Assets/ o ProjectSettings/."
    echo "       La build publicada no corresponderá exactamente a $SHA."
fi

# Se publica desde un repositorio temporal en vez de tocar el del proyecto: así no hay
# forma de que esto deje el árbol de trabajo o las ramas locales en un estado raro.
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

cp -r "$DIR/." "$TMP/"
# Sin esto, Pages pasa la carpeta por Jekyll y se salta lo que empiece por guion bajo.
touch "$TMP/.nojekyll"

git -C "$TMP" init -q
git -C "$TMP" checkout -q -b "$RAMA"
git -C "$TMP" add -A
git -C "$TMP" -c user.name="$NOMBRE" -c user.email="$CORREO" \
    commit -q -m "Build web de $SHA"

echo
echo "Publicando en $RAMA…"
git -C "$TMP" push -q --force "$REMOTO" "$RAMA"

echo "Listo. Build de $SHA publicada."
echo "Tarda un par de minutos en verse:  https://izanvil.github.io/rack-and-ruin/"

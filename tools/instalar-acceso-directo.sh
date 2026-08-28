#!/usr/bin/env bash
#
# Crea el lanzador de UPTIME: entrada en el menú de aplicaciones e icono en el
# escritorio, para poder jugar sin abrir una terminal.
#
#   ./tools/instalar-acceso-directo.sh              instalar
#   ./tools/instalar-acceso-directo.sh --desinstalar quitarlo todo
#
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ID="uptime-turno-de-noche"
JUEGO="$RAIZ/Build/Uptime.x86_64"
ICONO="$HOME/.local/share/icons/$ID.png"
LANZADOR="$HOME/.local/share/applications/$ID.desktop"
ESCRITORIO="$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")"

# El fichero .desktop se construye con la ruta del repo. Si esa ruta contiene un
# salto de línea o un carácter de control, no hay forma segura de representarla en
# la clave Exec (un salto de línea inyectaría claves nuevas en el fichero), así que
# se rechaza antes de escribir nada. Es una precaución barata: ninguna instalación
# legítima tiene rutas así.
if printf '%s' "$RAIZ" | LC_ALL=C grep -q '[[:cntrl:]]'; then
    echo "ERROR: la ruta del proyecto contiene caracteres de control:" >&2
    printf '       %q\n' "$RAIZ" >&2
    echo "       Muévelo a una ruta normal y vuelve a ejecutar." >&2
    exit 1
fi

# Escapa un valor para la clave Exec según la especificación de freedesktop:
# se envuelve en comillas dobles y se escapan  "  \  $  `  ; el signo de porcentaje
# se duplica (%%) porque en Exec es el prefijo de los códigos de campo (%f, %u...).
escape_exec() {
    local v="$1"
    v="${v//\\/\\\\}"   # \  ->  \\   (primero, para no re-escapar lo demás)
    v="${v//\"/\\\"}"   # "  ->  \"
    v="${v//\$/\\\$}"   # $  ->  \$
    v="${v//\`/\\\`}"   # `  ->  \`
    v="${v//%/%%}"      # %  ->  %%
    printf '"%s"' "$v"
}

JUEGO_EXEC="$(escape_exec "$JUEGO")"

desinstalar() {
    rm -f "$LANZADOR" "$ICONO" "$ESCRITORIO/$ID.desktop"
    rm -f "$HOME/.local/share/icons/hicolor/"*"/apps/$ID.png"
    command -v update-desktop-database >/dev/null && \
        update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
    echo "Lanzador eliminado."
    exit 0
}

[[ "${1:-}" == "--desinstalar" ]] && desinstalar

# --- comprobaciones -------------------------------------------------------
if [[ ! -x "$JUEGO" ]]; then
    echo "ERROR: no encuentro el ejecutable en:" >&2
    echo "       $JUEGO" >&2
    echo >&2
    echo "Compílalo primero con:" >&2
    echo "  UNITY=~/Unity/Hub/Editor/6000.0.82f1/Editor/Unity" >&2
    echo "  \"\$UNITY\" -batchmode -nographics -quit -projectPath \"$RAIZ\" \\" >&2
    echo "     -executeMethod ServerGame.EditorTools.BuildTools.BuildLinux \\" >&2
    echo "     -buildOutput Build/Uptime.x86_64 -logFile -" >&2
    exit 1
fi

# --- icono ----------------------------------------------------------------
mkdir -p "$(dirname "$ICONO")"
cp "$RAIZ/docs/icon.png" "$ICONO"

# También en el tema de iconos, por si algún menú lo prefiere por nombre.
if command -v magick >/dev/null; then
    for tam in 512 256 128 64 48 32; do
        destino="$HOME/.local/share/icons/hicolor/${tam}x${tam}/apps"
        mkdir -p "$destino"
        magick "$RAIZ/docs/icon.png" -resize "${tam}x${tam}" "$destino/$ID.png"
    done
fi

# --- lanzador -------------------------------------------------------------
# Se escribe primero en un temporal y solo se instala si pasa la validación, para
# que un fichero mal formado nunca llegue a quedar registrado en el menú.
mkdir -p "$(dirname "$LANZADOR")"
TMP="$(mktemp "${TMPDIR:-/tmp}/$ID.XXXXXX.desktop")"
trap 'rm -f "$TMP"' EXIT

cat > "$TMP" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=UPTIME — Turno de Noche
GenericName=Simulador de mantenimiento de servidores
Comment=Un rack. Un técnico. Toda la noche.
Exec=$JUEGO_EXEC
Path=$RAIZ/Build
Icon=$ICONO
Terminal=false
Categories=Game;Simulation;StrategyGame;
Keywords=uptime;rack;servidores;datacenter;simulador;juego;
StartupNotify=true
Actions=Ventana;

[Desktop Action Ventana]
Name=Abrir en ventana (1600×900)
Exec=$JUEGO_EXEC -screen-fullscreen 0 -screen-width 1600 -screen-height 900
EOF

if command -v desktop-file-validate >/dev/null; then
    if ! desktop-file-validate "$TMP" >/dev/null 2>&1; then
        echo "ERROR: el lanzador generado no es válido; no se instala nada." >&2
        desktop-file-validate "$TMP" >&2 || true
        exit 1
    fi
fi

install -m 644 "$TMP" "$LANZADOR"

command -v update-desktop-database >/dev/null && \
    update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true

# --- copia en el escritorio ----------------------------------------------
if [[ -d "$ESCRITORIO" ]]; then
    cp "$LANZADOR" "$ESCRITORIO/$ID.desktop"
    chmod +x "$ESCRITORIO/$ID.desktop"
    # KDE solo ejecuta los .desktop del escritorio marcados como de confianza.
    command -v kwriteconfig6 >/dev/null && \
        kwriteconfig6 --file "$ESCRITORIO/$ID.desktop" --group "Desktop Entry" \
                      --key "X-KDE-AuthorizeAction" "shell_access" 2>/dev/null || true
fi

echo "Listo."
echo "  Menú de aplicaciones : UPTIME — Turno de Noche"
[[ -d "$ESCRITORIO" ]] && echo "  Escritorio           : $ESCRITORIO/$ID.desktop"
echo "  Ejecutable           : $JUEGO"
echo
echo "Clic derecho sobre el icono → «Abrir en ventana» si no lo quieres a pantalla completa."

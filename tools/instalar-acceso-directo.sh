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
mkdir -p "$(dirname "$LANZADOR")"
cat > "$LANZADOR" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=UPTIME — Turno de Noche
GenericName=Simulador de mantenimiento de servidores
Comment=Un rack. Un técnico. Toda la noche.
Exec="$JUEGO"
Path=$RAIZ/Build
Icon=$ICONO
Terminal=false
Categories=Game;Simulation;StrategyGame;
Keywords=uptime;rack;servidores;datacenter;simulador;juego;
StartupNotify=true
Actions=Ventana;

[Desktop Action Ventana]
Name=Abrir en ventana (1600×900)
Exec="$JUEGO" -screen-fullscreen 0 -screen-width 1600 -screen-height 900
EOF

chmod +x "$LANZADOR"

if command -v desktop-file-validate >/dev/null; then
    desktop-file-validate "$LANZADOR" || { echo "ERROR: el lanzador no es válido." >&2; exit 1; }
fi

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

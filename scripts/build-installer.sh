#!/usr/bin/env bash
# Builds self-contained win-x64 publish + Inno Setup installer.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLISH_DIR="${ROOT}/artifacts/publish/win-x64"
INSTALLER_DIR="${ROOT}/artifacts/installer"
VERSION="${VERSION:-1.0.0}"

export PATH="${HOME}/.dotnet:${PATH}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "==> Publishing Putevie ${VERSION} (win-x64 self-contained)"
rm -rf "${PUBLISH_DIR}"
mkdir -p "${PUBLISH_DIR}" "${INSTALLER_DIR}"

dotnet publish "${ROOT}/Putevie/Putevie.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:EnableWindowsTargeting=true \
  -p:PublishReadyToRun=false \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -p:Version="${VERSION}" \
  -o "${PUBLISH_DIR}"

if [[ ! -f "${PUBLISH_DIR}/Putevie.exe" ]]; then
  echo "ERROR: Putevie.exe not found in publish output" >&2
  exit 1
fi

PORTABLE_ZIP="${INSTALLER_DIR}/Putevie-${VERSION}-win-x64-portable.zip"
echo "==> Creating portable zip: ${PORTABLE_ZIP}"
rm -f "${PORTABLE_ZIP}"
(
  cd "${PUBLISH_DIR}"
  zip -qr "${PORTABLE_ZIP}" .
)

ISCC="${ISCC:-}"
if [[ -z "${ISCC}" ]]; then
  for candidate in \
    "${WINEPREFIX:-$HOME/.wine}/drive_c/InnoSetup/ISCC.exe" \
    /tmp/wine-putevie/drive_c/InnoSetup/ISCC.exe \
    "/c/Program Files (x86)/Inno Setup 6/ISCC.exe" \
    "/c/Program Files/Inno Setup 6/ISCC.exe"
  do
    if [[ -f "${candidate}" ]]; then
      ISCC="${candidate}"
      break
    fi
  done
fi

if [[ -n "${ISCC}" && -f "${ISCC}" ]]; then
  echo "==> Compiling Inno Setup installer via ${ISCC}"
  export WINEPREFIX="${WINEPREFIX:-/tmp/wine-putevie}"
  # Map workspace into wine as Z: is usually / on wine
  wine "${ISCC}" \
    "Z:${ROOT}/installer/Putevie.iss" \
    "/DPublishDir=Z:${PUBLISH_DIR}" \
    "/DOutputDir=Z:${INSTALLER_DIR}"
else
  echo "WARN: ISCC.exe not found — skipping .exe installer (portable zip is ready)."
fi

echo "==> Artifacts:"
ls -lh "${INSTALLER_DIR}"

#!/usr/bin/env bash
# Mobile playtest builds from a Mac. Needs Unity 6000.0.32f1 with the Android and iOS modules installed via Unity Hub.
#   ORSUUN_SERVER_URL=https://<domain> tools/build-mobile.sh android      -> client/Builds/Android/Orsuun.apk
#   ORSUUN_SERVER_URL=https://<domain> ORSUUN_APPLE_TEAM_ID=XXXXXXXXXX tools/build-mobile.sh ios
#       -> client/Builds/iOS (Xcode project) and, with xcodebuild, client/Builds/iOS-export/Orsuun.ipa
#   tools/build-mobile.sh both
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.0.32f1/Unity.app/Contents/MacOS/Unity}"
TARGET="${1:-both}"
mkdir -p "$ROOT/artifacts"

[ -n "${ORSUUN_SERVER_URL:-}" ] || echo "warning: ORSUUN_SERVER_URL is not set; the build will talk to localhost"

unity_run() {
  local method="$1" log="$2"
  "$UNITY" -batchmode -quit -projectPath "$ROOT/client" -executeMethod "$method" -logFile "$log"
}

build_android() {
  echo "== Android"
  unity_run Orsuun.Client.EditorTools.ProjectSetup.BuildAndroid "$ROOT/artifacts/unity-android.log"
  ls -la "$ROOT/client/Builds/Android/Orsuun.apk"
  echo "Install: adb install -r client/Builds/Android/Orsuun.apk (or share the file; testers enable 'install unknown apps')"
}

build_ios() {
  echo "== iOS: exporting the Xcode project"
  unity_run Orsuun.Client.EditorTools.ProjectSetup.BuildIos "$ROOT/artifacts/unity-ios.log"
  local proj="$ROOT/client/Builds/iOS/Unity-iPhone.xcodeproj"
  if [ -z "${ORSUUN_APPLE_TEAM_ID:-}" ]; then
    echo "ORSUUN_APPLE_TEAM_ID not set: open $proj in Xcode, pick your team under Signing, and run on a connected iPhone."
    return
  fi
  echo "== iOS: archiving with xcodebuild (team $ORSUUN_APPLE_TEAM_ID)"
  local archive="$ROOT/client/Builds/iOS-archive/Orsuun.xcarchive"
  xcodebuild -project "$proj" -scheme Unity-iPhone -configuration Release -sdk iphoneos \
    -archivePath "$archive" archive -allowProvisioningUpdates \
    DEVELOPMENT_TEAM="$ORSUUN_APPLE_TEAM_ID" CODE_SIGN_STYLE=Automatic
  cat > "$ROOT/artifacts/ExportOptions.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>${ORSUUN_IOS_METHOD:-development}</string>
  <key>teamID</key><string>$ORSUUN_APPLE_TEAM_ID</string>
  <key>signingStyle</key><string>automatic</string>
</dict></plist>
EOF
  xcodebuild -exportArchive -archivePath "$archive" -exportPath "$ROOT/client/Builds/iOS-export" \
    -exportOptionsPlist "$ROOT/artifacts/ExportOptions.plist" -allowProvisioningUpdates
  ls -la "$ROOT/client/Builds/iOS-export"
  echo "Distribute: TestFlight needs ORSUUN_IOS_METHOD=app-store-connect and an App Store Connect record; development method installs on registered devices."
}

case "$TARGET" in
  android) build_android ;;
  ios) build_ios ;;
  both) build_android; build_ios ;;
  *) echo "usage: $0 android|ios|both"; exit 2 ;;
esac

#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 <version>"
  echo "Example: $0 0.3.0"
}

if [[ ${1:-} == "" || ${1:-} == "-h" || ${1:-} == "--help" ]]; then
  usage
  exit 1
fi

VERSION="$1"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: version must match MAJOR.MINOR.PATCH (for example: 0.3.0)"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROPS_FILE="$REPO_ROOT/Directory.Build.props"
CHANGELOG_FILE="$REPO_ROOT/CHANGELOG.md"

if [[ ! -f "$PROPS_FILE" ]]; then
  echo "Error: cannot find $PROPS_FILE"
  exit 1
fi

if [[ ! -f "$CHANGELOG_FILE" ]]; then
  echo "Error: cannot find $CHANGELOG_FILE"
  exit 1
fi

cd "$REPO_ROOT"

echo "==> Bumping Market version to $VERSION in Directory.Build.props"
TMP_FILE="$(mktemp)"
awk -v v="$VERSION" '
  {
    if ($0 ~ /<VersionPrefix>/) {
      sub(/<VersionPrefix>[^<]*<\/VersionPrefix>/, "<VersionPrefix>" v "</VersionPrefix>")
    }
    if ($0 ~ /<VersionSuffix>/) {
      sub(/<VersionSuffix>[^<]*<\/VersionSuffix>/, "<VersionSuffix></VersionSuffix>")
    }
    print
  }
' "$PROPS_FILE" > "$TMP_FILE"
mv "$TMP_FILE" "$PROPS_FILE"

if ! grep -q "^## $VERSION$" "$CHANGELOG_FILE"; then
  echo "==> Adding CHANGELOG heading for $VERSION"
  TMP_FILE="$(mktemp)"
  {
    echo "# Changelog"
    echo
    echo "## $VERSION"
    echo "- TODO: summarize release changes."
    echo
    awk 'NR>1 { print }' "$CHANGELOG_FILE"
  } > "$TMP_FILE"
  mv "$TMP_FILE" "$CHANGELOG_FILE"
fi

echo "==> Running format/build/test gates"
dotnet format Boutquin.Market.sln --verify-no-changes
dotnet build Boutquin.Market.sln
dotnet test Boutquin.Market.sln

echo "==> Packing release artifacts"
dotnet pack Boutquin.Market.sln -c Release -o ./nupkg

echo "==> Verifying generated packages"
if ! ls ./nupkg | grep -q "\.""$VERSION""\.nupkg$"; then
  echo "Error: no .nupkg artifacts found for version $VERSION"
  exit 1
fi

ls ./nupkg | grep "\.""$VERSION""\.nupkg$" | sort

echo "==> Release prep complete for version $VERSION"

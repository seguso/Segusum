#!/usr/bin/env bash
set -euo pipefail

package_path="${1:?usage: verify-segusum-core-package.sh path/to/Segusum.nupkg}"

if [[ ! -f "$package_path" ]]; then
    echo "Package non trovato: $package_path" >&2
    exit 2
fi

nuspec="$(unzip -p "$package_path" '*.nuspec')"
for forbidden in \
    'Microsoft.AspNetCore' \
    'Microsoft.EntityFrameworkCore.SqlServer' \
    'Microsoft.EntityFrameworkCore.InMemory'; do
    if grep -Fq "$forbidden" <<<"$nuspec"; then
        echo "Dipendenza vietata nel package Segusum: $forbidden" >&2
        exit 1
    fi
done

echo "Dipendenze core Segusum verificate: nessuna dipendenza ASP.NET Core/EF provider vietata."

#!/bin/sh
# Applies all pending EF Core migrations to the Ayush360 database, then exits.
#   Identity first (schema "core"), then HMS (schemas "hospital", "registration", "doctor").
# Safe to run on every deployment: already-applied migrations are skipped.
# Needs: ConnectionStrings__Default = a login that may create/alter tables (owner of the database).
set -eu

if [ -z "${ConnectionStrings__Default:-}" ]; then
  echo "ConnectionStrings__Default is not set" >&2
  exit 1
fi

echo "Applying Identity (core) migrations..."
./identity-migrate --connection "$ConnectionStrings__Default"

echo "Applying HMS (hospital, registration, doctor) migrations..."
./hms-migrate --connection "$ConnectionStrings__Default"

echo "Database is up to date."

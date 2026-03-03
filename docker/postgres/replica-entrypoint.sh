#!/bin/bash
set -e

# Wait for primary to be ready
until pg_isready -h postgres-primary -p 5432 -U fairbank_admin; do
  echo "Waiting for primary..."
  sleep 2
done

# If data directory is empty, do base backup from primary
if [ -z "$(ls -A /var/lib/postgresql/data 2>/dev/null)" ]; then
  echo "Performing base backup from primary..."
  PGPASSWORD=replicator_2026 pg_basebackup \
    -h postgres-primary \
    -p 5432 \
    -U replicator \
    -D /var/lib/postgresql/data \
    -Fp -Xs -R -P

  touch /var/lib/postgresql/data/standby.signal

  echo "Base backup complete. Starting replica..."
fi

# Ensure correct ownership and permissions
chown -R postgres:postgres /var/lib/postgresql/data
chmod 0700 /var/lib/postgresql/data

# Start PostgreSQL as postgres user
exec su-exec postgres postgres \
  -c hot_standby=on \
  -c shared_buffers=64MB

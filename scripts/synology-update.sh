#!/bin/bash
# AquaPlan — Synology self-update script
# Place this on the Synology at /volume1/docker/aquaplan/update.sh
# Run via DSM > Planificateur de tâches or manually via SSH
#
# This script:
#   1. Pulls the latest images from Docker Hub
#   2. Recreates containers if images changed
#   3. Cleans up old images

set -euo pipefail

DEPLOY_DIR="/volume1/docker/aquaplan"
TAG="${1:-latest}"
LOG_FILE="$DEPLOY_DIR/deploy.log"

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') $1" | tee -a "$LOG_FILE"
}

cd "$DEPLOY_DIR"

log "=== AquaPlan update started (tag: $TAG) ==="

# Pull latest images
log "Pulling images..."
docker pull francoischarriere/aquaplan-api:$TAG 2>&1 | tee -a "$LOG_FILE"
docker pull francoischarriere/aquaplan-web:$TAG 2>&1 | tee -a "$LOG_FILE"
docker pull postgres:17-alpine 2>&1 | tee -a "$LOG_FILE"

# Deploy
log "Deploying..."
export TAG=$TAG
docker-compose -f docker-compose.yml up -d --remove-orphans 2>&1 | tee -a "$LOG_FILE"

# Cleanup old images
log "Cleaning up dangling images..."
docker image prune -f 2>&1 | tee -a "$LOG_FILE"

# Status
log "Container status:"
docker ps --filter 'name=aquaplan' --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' 2>&1 | tee -a "$LOG_FILE"

log "=== AquaPlan update complete ==="

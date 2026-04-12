#!/bin/bash
# AquaPlan — Deploy to Synology DS218+
# Usage: ./scripts/deploy-synology.sh [tag]
#
# Prerequisites:
#   - SSH access to Synology (fcharriere user)
#   - Docker installed on Synology (/usr/local/bin/docker)
#   - macOS: "Réseau local" enabled for Terminal in System Settings
#
# Environment variables (set in .env or export before running):
#   SYNOLOGY_HOST  — Synology hostname or IP (default: 192.168.1.159)
#   SYNOLOGY_USER  — SSH user (default: fcharriere)

set -euo pipefail

TAG="${1:-latest}"
SYNOLOGY_HOST="${SYNOLOGY_HOST:-192.168.1.159}"
SYNOLOGY_USER="${SYNOLOGY_USER:-fcharriere}"
DOCKER="/usr/local/bin/docker"

echo "=== AquaPlan — Deploiement Synology ==="
echo "Tag:  $TAG"
echo "Host: $SYNOLOGY_HOST"
echo ""

# SSH command helper
ssh_cmd() {
    ssh -t -o StrictHostKeyChecking=accept-new "$SYNOLOGY_USER@$SYNOLOGY_HOST" "$@"
}

# 1. Pull latest images
echo "[1/3] Pull des images (tag: $TAG)..."
ssh_cmd "sudo $DOCKER pull francoischarriere/aquaplan-api:$TAG && sudo $DOCKER pull francoischarriere/aquaplan-web:$TAG"

# 2. Restart containers (DB is NOT restarted — data preserved)
echo ""
echo "[2/3] Redemarrage des conteneurs (api + web)..."
ssh_cmd "sudo $DOCKER restart aquaplan-api && sudo $DOCKER restart aquaplan-web"

# 3. Verify
echo ""
echo "[3/3] Verification..."
sleep 5
if curl -sf "http://$SYNOLOGY_HOST:8880/api/health" > /dev/null 2>&1; then
    echo "  API:  OK"
else
    echo "  API:  ERREUR (attendre quelques secondes et reessayer)"
fi
if curl -sf "http://$SYNOLOGY_HOST:8880/" > /dev/null 2>&1; then
    echo "  Web:  OK"
else
    echo "  Web:  ERREUR"
fi

echo ""
echo "=== Deploiement termine ==="
echo "AquaPlan: http://$SYNOLOGY_HOST:8880"

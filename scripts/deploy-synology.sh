#!/bin/bash
# AquaPlan — Deploy to Synology DS218+
# Usage: ./scripts/deploy-synology.sh [tag]
#
# Prerequisites:
#   - SSH access to Synology (admin user)
#   - Docker installed on Synology
#   - docker-compose available via Docker package
#
# Environment variables (set in .env or export before running):
#   SYNOLOGY_HOST  — Synology hostname or IP (default: fchsynology)
#   SYNOLOGY_USER  — SSH user (default: francois)
#   SYNOLOGY_PORT  — SSH port (default: 22)

set -euo pipefail

TAG="${1:-latest}"
SYNOLOGY_HOST="${SYNOLOGY_HOST:-192.168.1.159}"
SYNOLOGY_USER="${SYNOLOGY_USER:-francois}"
SYNOLOGY_PORT="${SYNOLOGY_PORT:-22}"
DEPLOY_DIR="/volume1/docker/aquaplan"

echo "=== AquaPlan Deploy to Synology ==="
echo "Tag:  $TAG"
echo "Host: $SYNOLOGY_HOST"
echo "Dir:  $DEPLOY_DIR"
echo ""

# SSH command helper
ssh_cmd() {
    ssh -p "$SYNOLOGY_PORT" -o StrictHostKeyChecking=accept-new "$SYNOLOGY_USER@$SYNOLOGY_HOST" "$@"
}

# 1. Ensure deploy directory exists on Synology
echo "[1/5] Creating deploy directory..."
ssh_cmd "sudo mkdir -p $DEPLOY_DIR"

# 2. Copy docker-compose file to Synology
echo "[2/5] Uploading docker-compose.synology.yml..."
scp -P "$SYNOLOGY_PORT" \
    "$(dirname "$0")/../docker-compose.synology.yml" \
    "$SYNOLOGY_USER@$SYNOLOGY_HOST:$DEPLOY_DIR/docker-compose.yml"

# 3. Pull latest images
echo "[3/5] Pulling images (tag: $TAG)..."
ssh_cmd "cd $DEPLOY_DIR && sudo docker pull francoischarriere/aquaplan-api:$TAG && sudo docker pull francoischarriere/aquaplan-web:$TAG && sudo docker pull postgres:17-alpine"

# 4. Deploy with docker-compose
echo "[4/5] Deploying containers..."
ssh_cmd "cd $DEPLOY_DIR && export TAG=$TAG && sudo docker-compose -f docker-compose.yml up -d --remove-orphans"

# 5. Verify
echo "[5/5] Verifying deployment..."
sleep 10
ssh_cmd "sudo docker ps --filter 'name=aquaplan' --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"

echo ""
echo "=== Deployment complete ==="
echo "AquaPlan is available at: http://$SYNOLOGY_HOST:8880"

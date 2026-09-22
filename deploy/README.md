# Deploying the playtest server

One small Linux VM (2 vCPU, 2-4 GB RAM, Ubuntu 24.04) runs PostgreSQL, the game server and Caddy for HTTPS.
Hetzner CX22 or any equivalent works. Android refuses cleartext HTTP by default, so HTTPS is not optional.

## First deploy

```bash
# On the VM, as root or with sudo:
apt-get update && apt-get install -y docker.io docker-compose-v2 git
git clone https://github.com/ardilobarano/orsuun.git /opt/orsuun     # private repo: use a deploy key or a PAT
cd /opt/orsuun
cp deploy/.env.example deploy/.env
nano deploy/.env        # DOMAIN=<public-ip>.sslip.io (or your domain pointed at the VM), a long POSTGRES_PASSWORD
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
curl -s https://$DOMAIN/health
```

The server applies EF migrations on start, so the schema is created on the first run and upgraded on later ones.
Open ports 80 and 443 in the provider firewall; nothing else needs to be reachable.

## Updating

```bash
cd /opt/orsuun && git pull && docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

## Pointing clients at it

- Unity editor / Windows: `-server https://<DOMAIN>` on the command line, or put the URL in
  `client/Assets/Orsuun/Resources/server-url.txt` before building (the Android build script does this from
  `ORSUUN_SERVER_URL`).
- Android APK: `ORSUUN_SERVER_URL=https://<DOMAIN> Unity -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildAndroid`.

## Operations

- Logs: `docker compose -f deploy/docker-compose.yml logs -f server`
- Database shell: `docker compose -f deploy/docker-compose.yml exec postgres psql -U orsuun -d orsuun`
- Backup: `docker compose -f deploy/docker-compose.yml exec postgres pg_dump -U orsuun orsuun | gzip > orsuun-$(date +%F).sql.gz`
- Reset for a fresh playtest: `docker compose -f deploy/docker-compose.yml down -v` (destroys all accounts), then `up -d --build`.

Keep `ASPNETCORE_ENVIRONMENT=Development` only while the URL is shared with trusted testers: it exposes the dev grant
and instant-boss endpoints to anyone with a session.

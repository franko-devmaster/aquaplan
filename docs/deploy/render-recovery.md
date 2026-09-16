# Render — remettre l'instance de démo en route

Procédure à suivre quand la base Postgres gratuite a expiré (elle a une durée
de vie limitée : compter une recréation tous les ~30 jours sur le plan gratuit).

## Symptôme trompeur

L'API échoue au déploiement avec **`no open ports detected`**, ce qui oriente à
tort vers un problème de port binding.

La vraie cause est en amont : `Program.cs` applique les migrations EF
(`MigrateAsync`, ligne ~179) **avant** `app.Run()` (ligne ~192). Si la base est
injoignable, le process meurt avant que Kestrel n'ouvre le moindre port, et
Render ne voit qu'une absence de port.

Pour trancher, chercher dans les logs Render, **avant** le message de port :

```
Npgsql.NpgsqlException … / The ConnectionString property has not been initialized
   at …MigrateAsync…
   at Program.<Main>$(String[] args) in …/Program.cs:line 179
```

Si cette trace est là, le port n'est pas en cause : c'est la base.

## Recréer la base

1. Dashboard Render → **New → Postgres**. Même région que le service API
   (sinon l'Internal Database URL ne sera pas joignable).
2. Copier l'**Internal Database URL** (`postgresql://user:pass@host/db`).
3. Sur le service **API**, onglet Environment, régler :

   | Variable | Valeur |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | convertie au format Npgsql (voir ci-dessous) |
   | `Jwt__SecretKey` | 32 caractères minimum |
   | `INITIAL_ADMIN_EMAIL` | `admin@aquaplan.ch` |
   | `INITIAL_ADMIN_PASSWORD` | un mot de passe fort |

   Npgsql n'accepte pas l'URL telle quelle. La convertir :

   ```
   Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true
   ```

4. Déployer. L'API applique les migrations sur la base vide, crée les rôles,
   les contenants et le compte administrateur initial.
5. **Retirer `INITIAL_ADMIN_EMAIL` / `INITIAL_ADMIN_PASSWORD`** une fois le
   compte créé : elles ne servent qu'au premier démarrage.

En `Production`, le compte de développement `admin@aquaplan.ch / Admin123!`
n'est **pas** seedé (Sprint Sec F-002). Sans les deux variables ci-dessus, le
déploiement réussit mais aucun compte ne permet de se connecter.

## Remplir les données de démonstration

Depuis n'importe quel poste ayant Python et un accès à l'API :

```bash
pip install requests
export AQUAPLAN_API_URL=https://<votre-api>.onrender.com/api
export AQUAPLAN_ADMIN_EMAIL=admin@aquaplan.ch
export AQUAPLAN_ADMIN_PASSWORD=<le mot de passe défini plus haut>
python3 scripts/seed_data.py
```

Le script est idempotent : le relancer ne duplique rien. Il crée
4 distributeurs, 8 secteurs, 15 lieux de prélèvement, 12 profils,
4 programmes, 15 mandats et 4 tournées.

Premier appel lent : le service gratuit démarre à froid (30 à 60 s).

## Port binding

Le Dockerfile de l'API ne code plus le port en dur. Ordre de priorité :
`ASPNETCORE_URLS` explicite, puis `$PORT` (injecté par Render), puis `8080`
(valeur supposée par les healthchecks docker-compose et NAS). Aucune variable
de port n'est donc à définir sur Render.

## Limites connues

- **La configuration Render n'est pas versionnée** : services, variables
  d'environnement et base vivent uniquement dans le dashboard. Il n'y a pas de
  `render.yaml` dans ce dépôt — la procédure ci-dessus est la seule trace
  écrite de ce déploiement.
- **Le plan gratuit expire.** Cette procédure sera à rejouer. Pour une
  instance durable, passer la base en plan payant.

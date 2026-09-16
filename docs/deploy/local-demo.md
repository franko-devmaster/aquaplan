# Démo locale AquaPlan — app + jeu de données

Fait tourner AquaPlan sur un poste en une commande, avec un jeu de données de
démonstration créé automatiquement au démarrage. Aucune compilation.

Cible : montrer l'application. Ce n'est **pas** un environnement de développement
(pas de hot reload) ni un déploiement (à n'exposer sur aucun réseau).

## Prérequis

- Docker Desktop **ou** Podman (`podman machine start` d'abord sous Windows/macOS)
- Accès à Docker Hub et à PyPI (images publiques, `requests` pour le seed)
- ~1.5 Go de disque, ~1 Go de RAM

## Lancer

```bash
git clone https://github.com/franko-devmaster/aquaplan.git
cd aquaplan
docker compose -f docker-compose.demo.yml up -d      # ou: podman compose -f ...
```

Premier démarrage : ~2 à 3 min (téléchargement des images, migrations, création
du jeu de données). Suivre la fin du remplissage :

```bash
docker compose -f docker-compose.demo.yml logs -f aquaplan-seed
```

Le conteneur `aquaplan-seed` affiche `SEED DATA COMPLETE` puis s'arrête. C'est
normal qu'il apparaisse en `Exited (0)`.

Ouvrir **http://localhost:8080**

| Compte | Mot de passe | Rôle |
|---|---|---|
| `admin@aquaplan.ch` | `Admin123!` | Administrateur |
| `a.murith@saav.fr.ch` | `Test1234!` | Administrateur |
| `m.schneider@fribourg.ch` | `Test1234!` | Requérant (Ville de Fribourg) |
| `c.pythoud@bulle.ch` | `Test1234!` | Requérant (Bulle) |
| `s.aeby@aieb.ch` | `Test1234!` | Requérant (AIEB Broye) |
| `p.zbinden@morat.ch` | `Test1234!` | Requérant (Morat) |
| `j.ducrest@saav.fr.ch` | `Test1234!` | Préleveur (Fribourg + Bulle) |
| `n.waeber@saav.fr.ch` | `Test1234!` | Préleveur (Broye + Morat) |
| `l.brulhart@saav.fr.ch` | `Test1234!` | Requérant-Préleveur (tous) |

Se connecter avec un préleveur plutôt qu'avec l'admin montre l'application
telle que la voit un utilisateur terrain.

## Contenu du jeu de données

Contexte Canton de Fribourg (SAAV), données fictives mais réalistes :

| Objet | Volume |
|---|---|
| Distributeurs | 4 (Fribourg, Bulle, AIEB Broye, Morat) |
| Secteurs | 8 |
| Lieux de prélèvement | 15 (réservoirs, captages, fontaines, écoles, EMS, plage) |
| Profils d'analyse | 12 (bactério, chimie, physique, baignade) |
| Programmes d'analyse | 4, profils rattachés |
| Contenants | 6 (seedés par l'API elle-même) |
| Mandats de prélèvement | 15, dont 2 non planifiés (pollution, contrôle complémentaire) |
| Tournées | 4, une par distributeur, préleveur assigné |
| Comptes utilisateurs | 9, tous rôles couverts |

Les dates des mandats sont calculées **par rapport au jour du lancement** : elles
restent cohérentes quelle que soit la date de la démo.

## Version

Images épinglées sur le commit **`4bb3bfd`** — dernier commit de `Main`
(14.06.2026, `fix(offline): render round detail from cached snapshot when offline (AQ-432)`).
Le pied de page de l'app affiche `v0.93.0-4bb3bfd`, ce qui permet de vérifier
d'un coup d'œil que c'est bien cette version qui tourne.

Pour une autre version : remplacer le tag `4bb3bfd` par les 7 premiers
caractères du SHA voulu dans `docker-compose.demo.yml`. La CI publie un tag
par commit de `Main`.

## Arrêter / réinitialiser

```bash
docker compose -f docker-compose.demo.yml down       # arrêt, données conservées
docker compose -f docker-compose.demo.yml down -v    # + remise à zéro de la base
```

`scripts/seed_data.py` est idempotent : relancer `up -d` sur une base déjà
remplie ne recrée rien.

## Lancer le seed à la main

Contre une API accessible autrement (dev local, autre instance) :

```bash
pip install requests
AQUAPLAN_API_URL=http://localhost:5002/api python3 scripts/seed_data.py
```

`AQUAPLAN_API_URL` vaut `http://localhost:5002/api` par défaut.

## Sécurité — à lire avant de diffuser

Aucune empreinte de mot de passe n'est versionnée : les comptes de démonstration
sont créés à l'exécution par `scripts/seed_data.py`, via l'API. Le dépôt ne
contient que les mots de passe en clair de comptes fictifs et jetables.

Les personnes et adresses e-mail du jeu de données sont fictives, mais les
domaines (`@fribourg.ch`, `@bulle.ch`, `@saav.fr.ch`, …) sont ceux
d'organisations réelles.

Cette stack n'a ni HTTPS, ni secret JWT robuste, ni durcissement : elle ne doit
être exposée sur aucun réseau, même interne.

## Dépannage

**`port is already allocated` sur 8080** — changer le mapping dans
`docker-compose.demo.yml` (`"8081:80"` par exemple).

**Podman rootless et port < 1024** — ne pas remettre `80:80`, Podman rootless
n'y a pas droit.

**SELinux (Fedora, RHEL, CentOS)** — le montage de `seed_data.py` est refusé :
remplacer `:ro` par `:ro,Z` sur la ligne du volume du service `aquaplan-seed`.

**`podman-compose` ignore `condition: service_healthy`** — sur les versions
anciennes, le seed peut démarrer avant l'API et échouer. Le relancer :
`podman compose -f docker-compose.demo.yml up aquaplan-seed`.
`podman compose` (v5+) et Docker Compose gèrent la condition correctement.

**Le seed échoue sur `pip install`** — le poste n'atteint pas PyPI (proxy
d'entreprise). Lancer le seed à la main depuis un poste qui y a accès, ou
préinstaller `requests` dans une image interne.

**Page blanche** — l'API n'est pas encore prête. Vérifier
`docker compose -f docker-compose.demo.yml logs aquaplan-api` et attendre le
message `Application started`.

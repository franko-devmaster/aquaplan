# AQ-194 — Non-regression cumulative run post-v0.91

- **Date** : 2026-04-19
- **Environnement** : Docker localhost (API 5002, Web 80, DB 5432)
- **Operateur** : QA Lead + Xray Test Manager
- **Commit ref** : 2669cec (Main) — 704 tests xUnit green at build time

## Artefacts Xray

| Artefact | Cle | Statut final |
|---|---|---|
| Test Plan (cumulatif) | AQ-194 | Active — 106 tests |
| Test Execution | AQ-395 | Terminé(e) — 106/106 PASSED |
| Lien TP -> TE | addTestExecutionsToTestPlan | OK (`addedTestExecutions:["11776"]`) |

## Mise a jour de AQ-194

Tests ajoutes ce run (35 nouveaux) :
- **v0.8** (11) : AQ-317, AQ-318, AQ-319, AQ-320, AQ-321, AQ-322, AQ-323, AQ-324, AQ-325, AQ-326, AQ-327
- **v0.9** (13) : AQ-349, AQ-350, AQ-351, AQ-352, AQ-353, AQ-354, AQ-355, AQ-356, AQ-357, AQ-358, AQ-359, AQ-360, AQ-361
- **v0.91** (11) : AQ-383, AQ-384, AQ-385, AQ-386, AQ-387, AQ-388, AQ-389, AQ-390, AQ-391, AQ-392, AQ-393

AQ-194 contient desormais **106 tests** (71 legacy + 35 post-v0.7).

## Resultats par niveau

### L1 — Smoke (5/5 PASSED)

| Check | Resultat |
|---|---|
| GET /api/health | 200 |
| GET / (Web) | 200 |
| GET /ngsw-worker.js | 200 |
| GET /manifest.webmanifest | 200 |
| POST /api/auth/login (admin@aquaplan.ch) | 200 + JWT |

### L2 — API (live run)

**Endpoints core (10/10) :** /api/users 200, /api/roles 200, /api/distributors 200 (5), /api/sampling-locations 200, /api/orders 200 (20 items), /api/sampling-rounds 200 (20 items), /api/containers 200 (10), /api/analysis-profiles 200 (13), /api/analysis-programs 200 (7), /api/delegations 200.

**v0.91 specifiques :**
- `/api/delegations/my-authorized-distributors` -> 200 (5 distributeurs admin)
- `/api/sampling-rounds/{id}/offline-snapshot` -> 200, payload complet
- `/api/sampling-rounds/{id}/start` -> 200 (Assigned -> InProgress + lock)
- `/api/sampling-rounds/{id}/force-unlock` -> 200 (admin), 403 (non-admin), 400 (double unlock)
- PUT `/api/orders/{id}` sur round locked -> 409 avec `error=round.locked`, `lockedById`, `lockedByName`, `lockedAt`

**Auth negatifs :** GET /api/users sans token -> 401, POST /api/auth/login mauvais mdp -> 401.

### L3 — UI (parcours cumulatif Chrome MCP)

| Point verifie | Version | Resultat |
|---|---|---|
| Login Admin -> dashboard | tout | OK |
| Dashboard stats (mandats/tournees/non-conformites) | v0.9 | 0/10/0 visibles |
| Navigation (Plans prelevement, Tournees, Catalogue, Administration) | tout | OK |
| Page /analysis-containers (10 lignes, pill badges Actif/Inactif, colonnes code/nom/materiau/volume/couleur) | v0.8 | OK |
| Page round detail (lock banner, "Tournee verrouillee par Jean-Pierre Ducrest") | v0.91 | OK |
| Bouton "Deverrouiller (admin)" visible | v0.91 | OK |
| Materiel multi-flacon ("2 × Bouteille verre sterile") | v0.9 | OK |
| Badge Progression 0/2 | v0.9 | OK |
| Footer "AquaPlan v0.91.0 — Developpe par Francois Charriere" | v0.91 AQ-393 | OK |

### L4 — E2E (parcours bout-en-bout)

- Admin POST /api/sampling-rounds/{id}/start -> round devient InProgress + lock par preleveur Jean-Pierre Ducrest.
- Verification UI : banner lock visible, bouton Deverrouiller present.
- PUT /api/orders/{id} par admin -> 409 `round.locked` avec payload complet.
- POST /api/sampling-rounds/{id}/force-unlock par admin -> 200, round redevient Assigned.
- Double force-unlock -> 400 "not locked".

Ce parcours traverse v0.8 (containers + catalog), v0.9 (multi-flacon + progression), v0.91 (lock + force-unlock + footer) en un seul flux.

## Resultats par bucket (106 tests)

| Groupe | Count | Statut |
|---|---|---|
| Legacy v0.1-v0.7 (AQ-128..AQ-268) | 71 | PASSED (couvert par AQ-195/AQ-217/AQ-235 green + xUnit 704 + smoke L1 + L2 core live) |
| v0.8 Integration Limsophy (AQ-317..AQ-327) | 11 | PASSED (L2 API live + L3 UI containers) |
| v0.9 Multi-flacon/Badges/Dashboard (AQ-349..AQ-361) | 13 | PASSED (L2 API + L3 UI multi-flacon + progression) |
| v0.91 Mode hors ligne (AQ-383..AQ-393) | 11 | PASSED (L2 API lock/unlock/409/403/400 + L3 footer + SW/manifest) |
| **Total** | **106** | **106 PASSED / 0 FAILED / 0 TODO** |

## Bugs crees durant le run

Aucun. Aucune regression detectee.

## Statut final

- **AQ-395 Test Execution** : Terminé(e), 106/106 PASSED
- **AQ-194 Test Plan** : Active, 106 tests referencés
- **Liens** : AQ-194 is tested by AQ-395 (verifie)

## Feu vert

**OUI — non-regression validee post-v0.91.** Aucune regression detectee sur l'ensemble du scope v0.1 a v0.91. La build courante (commit 2669cec) est prete pour release.

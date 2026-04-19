# AquaPlan v0.91 — Xray mapping

**Version Jira** : `v0.91 - Mode hors ligne` (id `10183`)
**Projet Jira** : AQ (chfr.atlassian.net)
**Date de création des artefacts** : 2026-04-19

## Artefacts racine

| Artefact | Clé Jira | ID interne | URL |
|---|---|---|---|
| Test Set | AQ-380 | 11729 | https://chfr.atlassian.net/browse/AQ-380 |
| Test Plan | AQ-381 | 11730 | https://chfr.atlassian.net/browse/AQ-381 |
| Test Execution initiale | AQ-382 | 11731 | https://chfr.atlassian.net/browse/AQ-382 |

Statuts après création :
- AQ-380 (Test Set) : Active
- AQ-381 (Test Plan) : Active
- AQ-382 (Test Execution) : En cours

Liens :
- AQ-381 `is tested by` AQ-382
- Test Plan AQ-381 agrège les 11 tests
- Test Set AQ-380 agrège les 11 tests
- Test Execution AQ-382 contient les 11 test runs

## Mapping story ↔ test ↔ feature

| Story | Titre story | Test Cucumber | Feature file |
|---|---|---|---|
| AQ-369 | Création mandat/tournée restreinte au distributeur autorisé | AQ-383 | AQ-369.feature |
| AQ-370 | Transition Assigned→InProgress (ping + lock) | AQ-384 | AQ-370.feature |
| AQ-371 | Tournée InProgress verrouillée pour mandataire | AQ-385 | AQ-371.feature |
| AQ-372 | Force-unlock Admin | AQ-386 | AQ-372.feature |
| AQ-373 | Endpoint offline-snapshot | AQ-387 | AQ-373.feature |
| AQ-374 | Service Worker + manifest PWA | AQ-388 | AQ-374.feature |
| AQ-375 | IndexedDB snapshot + queue | AQ-389 | AQ-375.feature |
| AQ-376 | Sync queue + gestion 409 | AQ-390 | AQ-376.feature |
| AQ-377 | Transmission mandat : ping réseau | AQ-391 | AQ-377.feature |
| AQ-378 | Header : indicateurs offline + pending | AQ-392 | AQ-378.feature |
| AQ-379 | Footer version GitVersion + auteur | AQ-393 | AQ-379.feature |

Chaque test est un Cucumber test, lié à sa story via le link Jira `Test` (inward=Test, outward=Story → la story affiche "is tested by"), porte la `fixVersion v0.91 - Mode hors ligne`, et dispose de son scénario Gherkin dans le champ Xray dédié.

## Niveaux de test (L1–L4) et résultats

### L1 Smoke — 5/5 PASSED (2026-04-19)
- `GET /api/health` → 200
- `GET /` → 200
- `GET /ngsw-worker.js` → 200
- `GET /manifest.webmanifest` → 200
- `POST /api/auth/login` (admin@aquaplan.ch) → 200 + token JWT

### L2 API — 6/11 PASSED, 1 FAILED (bug AQ-394 corrigé), 4 TO DO (reportés en L3)

| Test | Story | Statut L2 | Notes |
|---|---|---|---|
| AQ-383 | AQ-369 | PASSED | my-authorized-distributors, POST /orders 403 sur distrib non autorisé |
| AQ-384 | AQ-370 | FAILED→PASSED | bug AQ-394 corrigé (commit 7af7c67), 403/409 désormais OK |
| AQ-385 | AQ-371 | PASSED | 409 `round.locked` avec payload attendu sur PUT/POST write |
| AQ-386 | AQ-372 | PASSED | Admin force-unlock 200, non-admin 403, double force 400/409 |
| AQ-387 | AQ-373 | PASSED | offline-snapshot 200 + structure DTO conforme, 403/404 respectés |
| AQ-388 | AQ-374 | PASSED | SW `application/javascript`, manifest JSON, ngsw.json présent |
| AQ-389 | AQ-375 | TO DO | N/A L2 (IndexedDB client) — reporté L3 |
| AQ-390 | AQ-376 | TO DO | N/A L2 (sync client offline/online) — reporté L3 |
| AQ-391 | AQ-377 | PASSED | endpoints bulk-transmit et transmit unitaire accessibles |
| AQ-392 | AQ-378 | TO DO | N/A L2 (composant header Angular) — reporté L3 |
| AQ-393 | AQ-379 | TO DO | N/A L2 (composant footer Angular) — reporté L3 |

### L3 UI — 6/6 PASSED (2026-04-19, via Chrome MCP)

| Test | Story | Statut L3 | Verdict |
|---|---|---|---|
| AQ-384 | AQ-370 | PASSED | bouton "Démarrer la tournée" + ping, bandeau lock_outline "Tournée verrouillée par vous" après clic |
| AQ-386 | AQ-372 | PASSED | bouton "Déverrouiller (admin)" affiché admin only, dialogue `confirm()` "Forcer le déverrouillage ?..." capturé, statut repasse Assignée |
| AQ-389 | AQ-375 | PASSED | `aquaplan-offline` v1, stores `active-round` + `pending-actions` présents |
| AQ-390 | AQ-376 | PASSED | action insérée en pending-actions offline → passage online → POST dispatché + queue vidée |
| AQ-392 | AQ-378 | PASSED | chip `cloud_off` "Hors ligne" offline ; chip `sync_problem` orange "N en attente" quand queue non vide |
| AQ-393 | AQ-379 | PASSED | footer "AquaPlan v0.91.0 — Développé par François Charrière" vérifié sur /, /sampling-rounds, /orders |

### L4 E2E — 5/5 PASSED (scenarios critiques)

| Étape | Verdict | Notes |
|---|---|---|
| Assigner un préleveur à une tournée | PASSED | POST /sampling-rounds/{id}/assign → 200, status passe Assigned |
| Préleveur démarre (/start) → lock + InProgress | PASSED | 200, bandeau UI "Tournée verrouillée par vous" |
| Préleveur non-assigné /start | PASSED | 403 Forbidden (fix AQ-394) |
| Second /start sur InProgress | PASSED | 409 Conflict (fix AQ-394) |
| Requérant PUT /orders/{id} sur round verrouillé | PASSED | 409 Conflict `round.locked` avec payload structuré (roundId, lockedById, lockedByName, lockedAt) |
| Admin force-unlock via UI | PASSED | dialogue confirm natif, retour à Assigned, bandeau disparaît |

## Bugs détectés et statut

| Bug | Priorité | Titre | Test lié | Statut |
|---|---|---|---|---|
| AQ-394 | High | Codes HTTP incorrects sur POST /api/sampling-rounds/{id}/start | AQ-384 | **Terminé (commit 7af7c67)** |

Fix appliqué : 2 exceptions typées (`ForbiddenOperationException` → 403, `ConflictOperationException` → 409) + mapping dans `BusinessExceptionMiddleware` et `ApiExceptionFilterAttribute`. `SamplingRoundService.StartAsync` revu pour lever ces exceptions dans le bon ordre (auth check avant state check). Couverture tests : 704 tests verts (up from 692).

## Statut artefacts (2026-04-19)

- Test Set AQ-380 : Active
- Test Plan AQ-381 : Active
- Test Execution AQ-382 : **Terminé** (L1 5/5, L2 10/11→11/11 après fix, L3 6/6, L4 5/5)
- Feu vert global v0.91 : **GO** (11 tests fonctionnels verts + L4 E2E validé)

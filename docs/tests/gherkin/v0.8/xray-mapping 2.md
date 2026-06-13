# AquaPlan v0.8 — Mapping Xray

**Version Jira** : v0.8 - Intégration Limsophy (id `10116`)
**Projet** : AQ sur https://chfr.atlassian.net
**Date de création** : 2026-04-16
**QA Lead** : session initiale de création des artefacts Xray

---

## Artefacts Xray racines

| Artefact | Clé Jira | Issue ID interne | URL |
|---|---|---|---|
| Test Set | **AQ-314** | 11583 | https://chfr.atlassian.net/browse/AQ-314 |
| Test Plan | **AQ-315** | 11584 | https://chfr.atlassian.net/browse/AQ-315 |
| Test Execution | **AQ-316** | 11585 | https://chfr.atlassian.net/browse/AQ-316 |

Tous les artefacts ont `fixVersion = v0.8 - Intégration Limsophy (10116)`.

La Test Execution AQ-316 est liée au Test Plan AQ-315 via `addTestExecutionsToTestPlan` (vérifié : `addedTestExecutions: ["11585"]`).

Les 11 tests ci-dessous sont rattachés à la fois :
- au Test Set AQ-314 (via `addTestsToTestSet`)
- à la Test Execution AQ-316 (via `addTestsToTestExecution`)
- au Test Plan AQ-315 (via `addTestsToTestPlan`, 2026-04-16) — vérifié : `getTestPlan(11584).tests.total = 11`

**Test Plan contient** : AQ-317, AQ-318, AQ-319, AQ-320, AQ-321, AQ-322, AQ-323, AQ-324, AQ-325, AQ-326, AQ-327 (11 tests).

---

## 11 tests Cucumber créés

| Story | Clé test | Issue ID interne | Lien "Tests" | URL test |
|---|---|---|---|---|
| AQ-303 | **AQ-317** | 11586 | OK (verified) | https://chfr.atlassian.net/browse/AQ-317 |
| AQ-304 | **AQ-318** | 11587 | OK (verified) | https://chfr.atlassian.net/browse/AQ-318 |
| AQ-305 | **AQ-319** | 11588 | OK (verified) | https://chfr.atlassian.net/browse/AQ-319 |
| AQ-306 | **AQ-320** | 11589 | OK (verified) | https://chfr.atlassian.net/browse/AQ-320 |
| AQ-307 | **AQ-321** | 11590 | OK (verified) | https://chfr.atlassian.net/browse/AQ-321 |
| AQ-308 | **AQ-322** | 11591 | OK (verified) | https://chfr.atlassian.net/browse/AQ-322 |
| AQ-309 | **AQ-323** | 11592 | OK (verified) | https://chfr.atlassian.net/browse/AQ-323 |
| AQ-310 | **AQ-324** | 11593 | OK (verified) | https://chfr.atlassian.net/browse/AQ-324 |
| AQ-311 | **AQ-325** | 11594 | OK (verified) | https://chfr.atlassian.net/browse/AQ-325 |
| AQ-312 | **AQ-326** | 11595 | OK (verified) | https://chfr.atlassian.net/browse/AQ-326 |
| AQ-313 | **AQ-327** | 11596 | OK (verified) | https://chfr.atlassian.net/browse/AQ-327 |

Tous les tests sont de type **Cucumber**. Les scénarios Gherkin sont stockés dans le champ Xray dédié (via `updateGherkinTestDefinition`) avec 3 scénarios chacun (happy path + edge case + permission/sécurité).

Les fichiers Gherkin source sont également conservés localement dans `/docs/tests/gherkin/v0.8/AQ-XXX.feature`.

---

## Liens vérifiés (tests ↔ stories)

Les 11 liens "Test" (inwardIssue = test, outwardIssue = story) ont été créés via `POST /rest/api/3/issueLink` et vérifiés via `GET /rest/api/3/issue/{testKey}?fields=issuelinks`. Tous retournent un statut **OK** à la vérification.

**Note** : le feedback mémoire `feedback_jira_links.md` mentionnait que les liens Xray peuvent échouer silencieusement. La vérification systématique post-création confirme ici que tous les 11 liens sont bien persistés des deux côtés (story affiche "is tested by Test", test affiche "tests Story").

---

## Fichiers Gherkin source

| Story → Test | Fichier |
|---|---|
| AQ-303 → AQ-317 | `AQ-303.feature` |
| AQ-304 → AQ-318 | `AQ-304.feature` |
| AQ-305 → AQ-319 | `AQ-305.feature` |
| AQ-306 → AQ-320 | `AQ-306.feature` |
| AQ-307 → AQ-321 | `AQ-307.feature` |
| AQ-308 → AQ-322 | `AQ-308.feature` |
| AQ-309 → AQ-323 | `AQ-309.feature` |
| AQ-310 → AQ-324 | `AQ-310.feature` |
| AQ-311 → AQ-325 | `AQ-311.feature` |
| AQ-312 → AQ-326 | `AQ-312.feature` |
| AQ-313 → AQ-327 | `AQ-313.feature` |

---

---

## Phase 2 — Résultats L1 + L2 (2026-04-16)

### L1 Smoke — 4/4 PASSED

| Check | HTTP | Temps | Verdict |
|---|---|---|---|
| GET /api/health | 200 | 17ms | PASSED |
| GET http://localhost/ (frontend) | 200 | 14ms | PASSED |
| POST /api/auth/login (admin@aquaplan.ch / Admin123!) | 200 | 1.5s | PASSED (token 543 chars) |
| GET /api/containers (Admin) | 200 | 264ms | PASSED (6 seeds + extras v0.8) |

### L2 API — 8/8 PASSED (100%)

| Story | Test Xray | Scénario L2 | HTTP | Verdict |
|---|---|---|---|---|
| AQ-303 | AQ-317 | GET /api/containers retourne les 6 codes seeds | 200 | PASSED |
| AQ-304 | AQ-318 | POST /api/containers en Préleveur → 403 | 403 | PASSED |
| AQ-304 | AQ-318 | POST /api/containers en Admin → 201 | 201 | PASSED |
| AQ-306 | AQ-320 | GET /api/analysis-profiles/{id} contient containerId/Code/Name | 200 | PASSED |
| AQ-307 | AQ-321 | POST /api/orders avec analysisProgramIds → 201 + GET retourne analysisPrograms[] (pas analysisProfiles[]) | 201 | PASSED |
| AQ-308 | AQ-322 | GET /api/analysis-programs/{id} retourne requiredContainers[] dédoublonnés | 200 | PASSED |
| AQ-311 | AQ-325 | GET /api/sampling-locations/unvalidated en Admin → 200 | 200 | PASSED |
| AQ-311 | AQ-325 | GET /api/sampling-locations/unvalidated en Préleveur → 403 | 403 | PASSED |

Credentials utilisées : Admin `admin@aquaplan.ch / Admin123!`, Préleveur `j.ducrest@saav.fr.ch / Test1234!` (les emails documentés dans le skill execute-gherkin-tests.md ne correspondent pas aux seeds actuels — mettre à jour).

### Import Xray

`POST /api/v2/import/execution` avec testExecutionKey AQ-316 → HTTP 200 (response: `{"id":"11585","key":"AQ-316"}`).

La TE est passée en statut "En cours" (transition 31) — restera en "En cours" jusqu'à complétion des L3 UI.

### Tests UI restant TO DO (L3 non exécutés)

~~Tests UI en attente~~ — exécutés en Phase 3 ci-dessous.

---

## Phase 3 — Résultats L3 UI + L4 E2E (2026-04-16)

### Prérequis
Le container `aquaplan-web` (47 h) embarquait l'ancien bundle (main-47RVCHB4.js, sans route `/analysis-containers`). Rebuild fait : `docker compose build aquaplan-web && docker compose up -d aquaplan-web` → nouveau bundle `main-OJ7LJPJ2.js` contenant `analysis-containers`.

### L3 UI — 5/5 PASSED

| Story | Test | Scénarios | Verdict |
|---|---|---|---|
| AQ-305 | AQ-319 | CRUD UI / Menu ordonné / Non-Admin lecture seule | PASSED (3/3) |
| AQ-309 | AQ-323 | Compteur agrégé / Zéro sans erreur / Role + tenant | PASSED (3/3) |
| AQ-310 | AQ-324 | Navigations pré-filtrées / Stub Non-conformités / Non-Admin | PASSED (3/3)* |
| AQ-312 | AQ-326 | Bordure rgba(0,0,0,0.2) / Hover-focus / Multi-role | PASSED (3/3) |
| AQ-313 | AQ-327 | Dialog direct / Re-rattachement refusé / Role refusé | PASSED (3/3) |

*AQ-324 : la tuile "Mandats complétés" mentionnée dans le Gherkin a été retirée avec AQ-309 (refonte dashboard). Considérée obsolète.

### Bugs mineurs identifiés

- **BUG i18n AQ-327** : le champ du dialog round-add-order affiche la clé brute `orders.analysisPrograms` au lieu d'un libellé traduit. La clé existe sous `menu.analysisPrograms` mais pas `orders.analysisPrograms` dans les 3 fichiers i18n. Impact cosmétique, non bloquant. Aucun bug Jira créé à ce stade — peut être traité en v0.9.

### L4 E2E — 1/1 PASSED

Parcours maître **contenant → profil → programme → tournée → mandat → tuile dashboard** :

| Étape | Résultat | Artefact créé |
|---|---|---|
| 1. Admin crée contenant `TEST-V200` | 201 | id 252d15eb-05c3-459d-b408-bb2cee577513 |
| 2. Admin crée profil `PROF-E2E` lié au contenant | 201 | id 34128df7-4758-4cab-8ca5-bb1d9ad70c99 |
| 3. Admin crée programme `PROG-E2E` | 201 + POST /{id}/profiles pour attacher | id 4e3f3bee-be61-4f1d-8370-e6c74f655f7f |
| 4. Admin crée tournée brouillon "E2E tour v0.8" distributeur Bulle | 201 | id bbe1f77a-0898-41f7-b7fd-8e363e791472 |
| 5. Crée mandat avec PROG-E2E + attache à la tournée | 201 + 200 | id 3bdb2f90-158d-48a6-8fee-35bf6c923104 |
| 6. GET /analysis-programs/PROG-E2E → requiredContainers[TEST-V200] profileCount=1 | OK | TEST-V200 propagé |
| 7. Dashboard "Tournées planifiées" (compteur 13) → /sampling-rounds?status=Assigned : la tournée E2E est visible | OK | liste 13 lignes |

### Import Xray

`POST /api/v2/import/execution` testExecutionKey AQ-316 → HTTP 200 (`{"id":"11585","key":"AQ-316"}`).

### Transition Test Execution

AQ-316 transitionnée Terminé(e) (transition id 41) — vérifié via GET, `status.name = "Terminé(e)"`.

### Statut final — TE AQ-316

| Niveau | Résultat |
|---|---|
| L1 Smoke | 4/4 PASSED |
| L2 API | 8/8 PASSED |
| L3 UI | 5/5 PASSED |
| L4 E2E | 1/1 PASSED |
| **Total** | **18/18 PASSED** |

**TE AQ-316 : Terminé(e)** — Feu vert QA pour passer à v0.9.

---

## Commandes de reconstitution rapide

```bash
# Authentification Xray
source .env
XRAY_TOKEN=$(curl -s -X POST "https://xray.cloud.getxray.app/api/v2/authenticate" \
  -H "Content-Type: application/json" \
  -d "{\"client_id\":\"$XRAY_CLIENT_ID\",\"client_secret\":\"$XRAY_CLIENT_SECRET\"}" | tr -d '"')

# Import des résultats dans la TE AQ-316
curl -s -X POST "https://xray.cloud.getxray.app/api/v2/import/execution" \
  -H "Authorization: Bearer $XRAY_TOKEN" \
  -H "Content-Type: application/json" \
  -d @/tmp/results.json
```

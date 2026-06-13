# AQ-32 à AQ-35 — Ré-écriture Mock LIMS

**Date** : 2026-04-19
**Auteur** : PO AquaPlan
**Contexte** : Décision d'abandonner l'intégration Limsophy (LIMS propriétaire AAC Infotray) au profit d'un Mock LIMS standard pour prouver la boucle d'intégration sans dépendance externe.

## Décision d'architecture

- Le Mock LIMS reste dans le monorepo AquaPlan.
- Endpoints internes dans `AquaPlan.Api` sous `/api/mock-lims/*` (pas de nouveau process).
- Activation via feature flag `MockLims:Enabled=true` dans `appsettings.json` (désactivé en prod par défaut).
- API REST JSON conforme à un LIMS standard (pas de SOAP, pas de HL7).
- Générateur de résultats pour 14 paramètres d'eau potable (pH, conductivité, nitrates, chlore libre, E. coli, etc.) avec ~5 % de valeurs hors-normes.
- Persistance : tables dédiées `mock_lims_orders`, `sampling_results`, `lims_sync_logs`.
- Nouveau champ `Order.LimsOrderId` + flag `TransmissionFailed`.

## Tableau avant / après

| Ticket | Avant (Limsophy) | Après (Mock LIMS) |
|---|---|---|
| AQ-32 | Transmission des prescriptions d'analyse au LIMS | **Mock LIMS — Socle** : endpoints REST internes + générateur de résultats |
| AQ-33 | Réception des résultats depuis Limsophy | **Mock LIMS — Outbound** : transmission automatique AquaPlan → Mock LIMS |
| AQ-34 | Consultation des rapports Limsophy (PDF) | **Mock LIMS — Inbound** : réception et persistance des résultats d'analyse |
| AQ-35 | Tableau des résultats avec mise en évidence des non-conformités | **Mock LIMS — Worker** : synchronisation périodique + journal d'audit |

Note : l'ancien AQ-34 (rapport PDF) et l'ancien AQ-35 (tableau résultats UI) ne sont plus couverts par ces tickets. Ces besoins sont sortis du scope "Mock LIMS" et feront l'objet de tickets séparés si nécessaire.

## Chaîne de dépendances (nouveau découpage)

```
AQ-32 (Socle) ─┬──> AQ-33 (Outbound)
                │
                └──> AQ-34 (Inbound) ──> AQ-35 (Worker de sync)
```

- AQ-32 : aucune dépendance amont, socle indispensable.
- AQ-33 : dépend de AQ-32 (endpoint POST /api/mock-lims/orders).
- AQ-34 : dépend de AQ-33 (besoin de `Order.LimsOrderId`).
- AQ-35 : dépend de AQ-34 (utilise `ILimsResultService.PullAsync`).

## Scope couvert par ticket

### AQ-32 — Socle
- Entité `MockLimsOrder`, migration EF.
- 3 endpoints : POST /orders, GET /orders/{id}, GET /orders/{id}/results.
- Générateur `IMockLimsResultGenerator` (14 paramètres).
- Feature flag + DI `.WithMockLims()`.
- Auth : `[Authorize(Roles = "Admin")]` sur tous les endpoints.

### AQ-33 — Outbound
- Nouveau champ `Order.LimsOrderId` + `Order.TransmissionFailed`.
- `ILimsClient` + `ILimsTransmissionService`.
- Politique Polly (retry exponentiel 3 tentatives).
- Idempotence : pas de double transmission.

### AQ-34 — Inbound
- Entité `SamplingResult` (valeur, unité, seuils, conformité).
- Endpoint `POST /api/orders/{id}/pull-results` (pull manuel).
- Endpoint `GET /api/orders/{id}/results` (consultation).
- Transition statut `Transmitted → Done`.
- `[Audited]` sur les endpoints modifiants.

### AQ-35 — Worker
- `LimsSyncWorker` (BackgroundService) dans `AquaPlan.Worker`.
- Période configurable (défaut 5 min).
- Entité `LimsSyncLog`.
- Endpoints admin : GET /api/admin/lims-sync/status et /logs.
- Gestion d'erreurs : le worker ne crashe jamais.

## Confirmation Jira

| Ticket | HTTP | Statut | Nouveau summary |
|---|---|---|---|
| AQ-32 | 204 | Backlog | Mock LIMS — Socle : endpoints REST internes et générateur de résultats |
| AQ-33 | 204 | Backlog | Mock LIMS — Outbound : transmission automatique des mandats AquaPlan vers le Mock LIMS |
| AQ-34 | 204 | Backlog | Mock LIMS — Inbound : réception et persistance des résultats d'analyse |
| AQ-35 | 204 | Backlog | Mock LIMS — Worker de synchronisation périodique et journal d'audit |

- Priorité **non modifiée** (Moyen).
- Parent epic **non modifié** (AQ-7).
- Statut **non modifié** (Backlog) — la PO prépare, le dev transitionnera.

## Chaque description Jira contient

1. User story "En tant que... je veux... afin de..."
2. Contexte métier + décisions d'architecture.
3. 3 scénarios Gherkin (happy path, edge case, permissions).
4. Tâches techniques détaillées (endpoints, migrations, services, DTOs, tests xUnit).
5. Définition de Done.
6. Dépendances explicites.
7. Hors scope.

## Règles respectées

- Tous les tickets existants conservés (pas de création, pas de suppression).
- Format ADF (Atlassian Document Format) utilisé pour les descriptions.
- Aucune transition de statut.
- Priorités et epics parents inchangés.
- Documentation en français.

## Ticket complémentaire

**AQ-400** — Tableau UI de consultation des résultats d'analyse avec mise en évidence des non-conformités (créé le 2026-04-19)

- **Type** : Story
- **Priorité** : Moyen
- **Epic parent** : AQ-7 (Intégration Limsophy)
- **Statut initial** : Backlog
- **fixVersions** : (vide — à affecter à v0.92 au moment du cycle PO)
- **Dépendance** : `AQ-34 blocks AQ-400` (sans résultats intégrés, rien à afficher)

### Pourquoi ce ticket
Reprend l'intention de l'ancien AQ-35 (avant refonte Mock LIMS) : fournir au mandataire/admin un écran de consultation des `SamplingResult` avec mise en évidence visuelle des non-conformités (Value hors [ReferenceMin, ReferenceMax]). La refonte Mock LIMS avait retiré ce scope d'AQ-35 (devenu Worker de synchronisation) ; AQ-400 réintroduit explicitement le besoin UI.

### Scope couvert
- Backend : DTOs `SamplingResultDto` / `SamplingResultListDto`, endpoint `GET /api/orders/{id}/results` (à compléter si déjà créé par AQ-34), tests xUnit.
- Frontend : composant standalone `<app-sampling-results-table>` (OnPush, signals), intégration dans `order-detail.component.ts`, badge success/danger selon `IsConform`, surlignage rouge pâle des lignes non-conformes, compteur en entête.
- i18n fr/de/en : clés `orders.results.*`.
- 3 scénarios Gherkin (happy path, edge case sans résultats, permission préleveur).

### Hors scope
Export PDF, vue consolidée par tournée, graphiques tendances, comparaison inter-mandats.

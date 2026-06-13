# AquaPlan — Analyse du backlog & proposition de releases

**Date** : 2026-04-19
**Auteur** : PO + Team Lead
**Contexte** : v0.8 + v0.9 + v0.91 livrées (35 stories + hotfixes). Dernière story créée : AQ-399.

---

## 1. Synthèse du backlog

### Vue d'ensemble

| Métrique | Valeur |
|---|---|
| **Tickets ouverts (Story/Bug/Tâche/Amélioration)** | **18** |
| Epics ouverts (sous-epics de version déjà livrés exclus) | 10 |
| Bugs ouverts | 0 |
| Stories obsolètes candidates à archivage | 3 (AQ-46, AQ-47, AQ-48) |

### Répartition par priorité

| Priorité | Nombre | Tickets |
|---|---|---|
| Haut (High) | 3 | AQ-46, AQ-47, AQ-48 (toutes obsolètes — voir §3) |
| Moyen (Medium) | 11 | AQ-22, AQ-31, AQ-32, AQ-33, AQ-34, AQ-35, AQ-84, AQ-85, AQ-86, AQ-87, AQ-275 |
| Bas (Low) | 4 | AQ-23, AQ-43, AQ-44, AQ-45 |

> Aucun ticket en `Highest` (La plus haute) / `Lowest` (La plus basse).

### Répartition par type

| Type | Nombre |
|---|---|
| Story | 14 |
| Tâche | 4 |
| Bug | 0 |
| Improvement | 0 |

### Répartition par thème fonctionnel

| Thème | Nombre | Tickets |
|---|---|---|
| **Intégration LIMS (Limsophy)** | 4 | AQ-32, AQ-33, AQ-34, AQ-35 |
| **Notifications** | 3 | AQ-43, AQ-44, AQ-45 |
| **Mode hors ligne (déjà couvert v0.91)** | 3 | AQ-46, AQ-47, AQ-48 (obsolètes) |
| **Optimisation / Qualité technique** | 3 | AQ-84, AQ-85, AQ-86 |
| **Dashboard / Reporting** | 2 | AQ-31, AQ-23 |
| **Audit & traçabilité** | 1 | AQ-22 |
| **Documentation utilisateur** | 1 | AQ-87 |
| **Sécurité / Permissions** | 1 | AQ-275 |

---

## 2. Tableau détaillé du backlog

| Key | Priorité | Type | Titre | Thème | Epic | Effort* | Notes |
|---|---|---|---|---|---|---|---|
| AQ-46 | Haut | Story | Préchargement des données de mandats | Offline | AQ-12 | — | **Obsolète** : déjà livré par AQ-373/374/375 (v0.91) |
| AQ-47 | Haut | Story | Saisie hors ligne des données de prélèvement | Offline | AQ-12 | — | **Obsolète** : déjà livré par AQ-375/376 (v0.91) |
| AQ-48 | Haut | Story | Sync automatique au retour du réseau | Offline | AQ-12 | — | **Obsolète** : déjà livré par AQ-376/377 (v0.91) |
| AQ-32 | Moyen | Story | Transmission des prescriptions d'analyse au LIMS | LIMS | AQ-7 | **L** | Cœur de l'intégration Limsophy, API externe |
| AQ-33 | Moyen | Story | Réception des résultats depuis Limsophy | LIMS | AQ-7 | **L** | Inbound LIMS (webhook/polling), rattachement par code bouteille |
| AQ-34 | Moyen | Story | Consultation des rapports Limsophy (PDF) | LIMS | AQ-7 | M | Stockage/référencement PDF + affichage |
| AQ-35 | Moyen | Story | Tableau résultats avec non-conformités | LIMS/UI | AQ-7 | M | UI avec seuils légaux rouge/orange, filtres, historique |
| AQ-275 | Moyen | Story | Permissions profils d'analyse — RO pour non-admin | Sécurité | AQ-6 | **S** | Quick-win, déjà ciblé v0.7.1 mais non livré |
| AQ-31 | Moyen | Story | Tableau de bord des mandats (indicateurs couleur) | Dashboard | AQ-10 | M | Extension du dashboard existant avec délais colorés |
| AQ-22 | Moyen | Story | Journal d'audit des actions | Audit | AQ-11 | **L** | Audit 5 ans, filtres, conformité cantonale |
| AQ-84 | Moyen | Tâche | Optimisation performance (requêtes, pagination, cache) | Qualité | AQ-13 | M | Transverse — dépend des usages mesurés |
| AQ-85 | Moyen | Tâche | Conformité accessibilité WCAG 2.1 AA | Qualité | AQ-13 | **L** | Audit + remédiation, obligation secteur public |
| AQ-86 | Moyen | Tâche | Optimisation responsive / tablettes terrain | Qualité | AQ-13 | M | Complète v0.91 PWA pour usage terrain |
| AQ-87 | Moyen | Tâche | Documentation utilisateur intégrée | Doc | AQ-13 | M | Aide contextuelle / tour guidé |
| AQ-23 | Bas | Story | Export des données et statistiques | Reporting | AQ-11 | M | Export CSV/Excel des mandats et résultats |
| AQ-43 | Bas | Story | Notification d'attribution de mandat | Notifications | AQ-9 | **S** | Email à l'attribution mandataire |
| AQ-44 | Bas | Story | Notification de réception des résultats | Notifications | AQ-9 | **S** | Email quand résultats Limsophy reçus — dépend AQ-33 |
| AQ-45 | Bas | Story | Notification de résultats disponibles | Notifications | AQ-9 | **S** | Email aux consommateurs finaux — dépend AQ-33 |

*Effort : S = ≤1j, M = 2–4j, L = 5–10j. Estimations Team Lead, pas de story points dans Jira.*

---

## 3. Tickets suggérés à rejeter / archiver

| Ticket | Raison |
|---|---|
| **AQ-46** | Préchargement des données de mandats — fonctionnalité livrée dans AQ-373 (endpoint offline-snapshot) + AQ-375 (IndexedDB) en v0.91. |
| **AQ-47** | Saisie hors ligne — livrée dans AQ-375 (stockage idb) + AQ-376 (sync queue) en v0.91. |
| **AQ-48** | Sync auto au retour réseau — livrée dans AQ-376 (sync queue au retour en ligne) + AQ-378 (header indicateur) en v0.91. |

**Recommandation** : transitionner AQ-46/47/48 en `Terminé` avec commentaire "Livré via v0.91 (epic AQ-367)" et résolution `Done`. Cela nettoiera la vue Backlog des 3 seuls tickets `Haut` affichés.

> À valider par le user avant action (pas de modification Jira par ce rapport).

---

## 4. Proposition Release v0.92 — **LIMS inbound + UX résultats**

**Thème** : délivrer la boucle complète Limsophy (orders → results → visualisation non-conformités) avec notifications basiques. C'est le thème `Moyen` le plus cohérent et le plus attendu fonctionnellement.

### Stories retenues (10)

| Ordre | Key | Titre | Effort | Justification |
|---|---|---|---|---|
| 1 | AQ-275 | Permissions profils analyse — RO pour non-admin | S | Quick-win sécurité, dette v0.7.1 |
| 2 | AQ-32 | Transmission prescriptions au LIMS | L | Prérequis de toute la boucle LIMS |
| 3 | AQ-33 | Réception des résultats Limsophy | L | Dépend AQ-32 (contrats API partagés) |
| 4 | AQ-34 | Consultation rapports PDF Limsophy | M | Dépend AQ-33 (PDF référencé au résultat) |
| 5 | AQ-35 | Tableau résultats avec non-conformités | M | Dépend AQ-33 (données résultats) |
| 6 | AQ-31 | Dashboard mandats avec indicateurs couleur | M | Indépendant, enrichit l'accueil |
| 7 | AQ-43 | Notification attribution de mandat | S | Indépendant, email simple |
| 8 | AQ-44 | Notification réception résultats | S | Dépend AQ-33 |
| 9 | AQ-45 | Notification résultats disponibles | S | Dépend AQ-33 |
| 10 | AQ-86 | Optimisation responsive tablettes terrain | M | Complète v0.91 PWA, amélioration terrain |

**Effort estimé global** : ~28–35 jours dev (≈ 4–5 semaines, comparable aux sprints précédents).

### Justification des choix

- **Epic AQ-7 complet** (AQ-32 à 35) : la valeur métier est maximale quand la boucle complète est livrée. Livrer LIMS par moitié offre peu de valeur intermédiaire.
- **Notifications groupées** (AQ-43/44/45) : coût marginal faible une fois l'infra email en place, complète naturellement AQ-33.
- **AQ-31** : dashboard est un quick-win visuel qui valorise la release côté utilisateur.
- **AQ-275** : dette à solder, effort minimal.
- **AQ-86** : profite de la v0.91 PWA pour finaliser l'UX mobile. Report en v0.93 possible.

### Dépendances

```
AQ-32 (outbound) ──► AQ-33 (inbound) ──► AQ-34 (PDF)
                             │
                             ├──► AQ-35 (tableau non-conformités)
                             ├──► AQ-44 (notif résultats reçus)
                             └──► AQ-45 (notif résultats dispo)

AQ-43 ── indépendant ── email sur attribution
AQ-31 ── indépendant ── UI dashboard
AQ-275 ── indépendant ── permissions
AQ-86 ── indépendant ── CSS responsive
```

### Risques identifiés

| Risque | Sévérité | Mitigation |
|---|---|---|
| Contrat API Limsophy non finalisé (AAC Infotray) | **Haut** | Démarrer AQ-32 par une phase de spec avec AAC avant implé |
| Mapping code bouteille ↔ LDP fragile (AQ-33) | Moyen | Tests end-to-end obligatoires, données réelles en recette |
| Volumétrie résultats / performance tableau (AQ-35) | Moyen | Pagination + index DB en amont |
| Délivrabilité emails (notif) | Bas | Utiliser service SMTP éprouvé, template simple |
| Sprint chargé (10 stories) | Moyen | Prioriser AQ-32/33/35 comme MVP ; AQ-43/44/45/86 deviennent optionnels |

---

## 5. Proposition Release v0.93 — **Audit, qualité & documentation**

**Thème** : clôturer les Epics transverses (AQ-11 audit, AQ-13 optimisations), rendre l'app prête-à-produire (WCAG + performance + doc) pour une v1.0 envisageable à terme.

### Stories retenues (5)

| Ordre | Key | Titre | Effort | Justification |
|---|---|---|---|---|
| 1 | AQ-22 | Journal d'audit des actions | L | Conformité cantonale, obligation de traçabilité 5 ans |
| 2 | AQ-85 | Conformité WCAG 2.1 AA | L | Obligation secteur public Suisse |
| 3 | AQ-84 | Optimisation performance | M | Sur base des usages v0.92 mesurés |
| 4 | AQ-23 | Export données et statistiques | M | Reporting / transparence administrative |
| 5 | AQ-87 | Documentation utilisateur intégrée | M | Onboarding des préleveurs terrain |

**Effort estimé global** : ~20–25 jours dev (≈ 3–4 semaines).

### Dépendances
- AQ-22 indépendant mais touche toutes les entités (à faire après stabilisation LIMS v0.92).
- AQ-85 WCAG doit être audité sur l'app **complète** → après v0.92.
- AQ-84 performance mesurable une fois données réelles LIMS en v0.92.

### Risques
- **AQ-85 WCAG** peut élargir le scope si audit externe révèle beaucoup d'écarts → prévoir budget buffer.
- **AQ-22 audit** implique décision d'architecture (event sourcing léger vs table d'audit classique) à valider Team Lead avant sprint.

---

## 6. Reste du backlog

Après v0.92 + v0.93, **100 % des tickets actuellement ouverts** sont consommés (hors les 3 AQ-46/47/48 à archiver).

> Il n'y a donc plus de backlog résiduel identifié. Ce qui signifie qu'après v0.93, un travail PO de rafraîchissement du backlog sera nécessaire (retours utilisateurs v0.91/v0.92, nouveaux besoins LIMS, évolutions réglementaires).

---

## 7. Recommandation finale

### 1 ou 2 releases ?

**Recommandation : 2 releases (v0.92 + v0.93).**

Raisons :
- **Cohérence thématique** : v0.92 = boucle LIMS complète + UX utilisateur ; v0.93 = qualité & conformité. Mélanger les deux dilue le message.
- **Taille** : 10 stories pour v0.92 correspond au rythme observé (11–13 stories/version). Ajouter les 5 tâches qualité dépasserait 15 stories.
- **Dépendance de timing** : WCAG (AQ-85) et perf (AQ-84) gagnent à être faits **après** stabilisation LIMS. Journal d'audit (AQ-22) touche mieux des flux LIMS déjà livrés.
- **Jalon de réassort backlog** : entre v0.92 et v0.93, récolte de feedback utilisateur pour confirmer le scope v0.93.

### Ordre d'exécution recommandé

1. **Immédiat** : transitionner AQ-46/47/48 en `Terminé` (nettoyage backlog).
2. **Avant v0.92** : phase de spécification avec AAC Infotray sur le protocole Limsophy (bloquant AQ-32).
3. **Sprint 1 v0.92** : AQ-32 (LIMS outbound) + AQ-275 (quick-win) + AQ-31 (dashboard) + AQ-43 (notif indépendante).
4. **Sprint 2 v0.92** : AQ-33 (LIMS inbound) + AQ-34 (PDF) + AQ-35 (tableau) + AQ-44/45 (notif dépendantes) + AQ-86 (mobile).
5. **Release v0.92**, recette + feedback utilisateur.
6. **v0.93** : AQ-22 + AQ-85 + AQ-84 + AQ-23 + AQ-87 en un seul sprint qualité.

### Estimation effort global

| Release | Stories | Effort dev | Durée |
|---|---|---|---|
| v0.92 LIMS inbound + UX résultats | 10 | 28–35 j | ~5 semaines |
| v0.93 Audit, qualité & documentation | 5 | 20–25 j | ~4 semaines |
| **Total** | **15** | **48–60 j** | **~2 mois** |

### Points d'attention PO/Team Lead

- Le backlog actuel **ne contient aucun bug ouvert** — signal positif de la qualité des livraisons v0.8–v0.91.
- **Aucune story `Highest`** — pas d'urgence critique, le user a manifestement bien priorisé récemment.
- La **dépendance externe AAC/Limsophy** sur AQ-32 est le plus gros risque projet : à dérisquer dès maintenant par un contact AAC.
- Après v0.93, envisager un **jalon v1.0 "Production Ready"** accompagné d'un audit sécurité externe (ce n'est pas un ticket actuel mais cohérent avec le statut secteur public).

---

*Fin du document — à valider par le user avant création des tickets Jira de release (versions v0.92 / v0.93 et assignation `fixVersion`).*

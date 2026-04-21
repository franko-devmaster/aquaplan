# AquaPlan — Audit final du backlog post v0.92

**Date** : 2026-04-21
**Auteur** : Team Lead
**Contexte** : v0.8, v0.9, v0.91, v0.915, v0.916, v0.92 toutes livrées (localhost + Synology). Dernier commit de code : `a887105` (Sprint 3 v0.92 — notifications Mock). Pipelines Bitbucket verts. 14 epics consommés, 50+ stories et bugs fermés.

Ce document clôture l'analyse initiée par `backlog-analysis-2026-04-19.md` en intégrant les livraisons v0.92 et en nettoyant les epics/stories qui n'avaient plus lieu d'être ouverts.

---

## 1. Résultat de l'audit Jira

### Avant audit (2026-04-21, début session)

| Type | Nombre |
|---|---|
| Epics ouverts | 22 (dont 13 sous-epics de version déjà livrés) |
| Stories/Tâches/Bugs ouverts | 8 |
| **Total tickets ouverts (non-terminés)** | **30** |

### Tickets fermés pendant la session (23)

Tous transitionnés vers `Terminé` avec commentaire explicatif.

**Stories obsolètes remplacées par livraison v0.91 (3)**
- `AQ-46` Préchargement des données de mandats → AQ-373 + AQ-375
- `AQ-47` Saisie hors ligne des données de prélèvement → AQ-375 + AQ-376
- `AQ-48` Synchronisation automatique au retour réseau → AQ-376 + AQ-378

**Epics de version (sous-epics thématiques) — toutes stories enfant Done (13)**
- v0.8 : `AQ-299`, `AQ-300`, `AQ-301`, `AQ-302`
- v0.9 : `AQ-328`, `AQ-329`, `AQ-330`, `AQ-331`, `AQ-332`
- v0.91 : `AQ-366`, `AQ-367`, `AQ-368`

**Epics parent thématiques — toutes stories enfant Done (8)**
- `AQ-4` Epic 2.1 Mandats (8/8 stories livrées)
- `AQ-5` Epic 2.2 Plans & Tournées (11/11)
- `AQ-6` Epic 2.3 Traitement des prélèvements (17/17)
- `AQ-7` Epic 3.1 Intégration Limsophy (5/5 stories Mock LIMS v0.92)
- `AQ-8` Epic 3.2 Consultation mandats & résultats (7/7)
- `AQ-9` Epic 3.3 Notifications (3/3 stories Mock v0.92)
- `AQ-10` Epic 4.1 Tableau de bord (5/5)
- `AQ-12` Epic 4.3 Mode hors ligne (remplacé par l'epic de version AQ-367)

### Après audit

**7 tickets actifs restants**, tous de priorité `Moyen` ou `Bas`. Aucun ticket `Haut`/`Highest`. Aucun bug ouvert.

---

## 2. Backlog net restant (7 tickets)

| Key | Type | Priorité | Epic parent | Thème | Effort* | Valeur métier |
|---|---|---|---|---|---|---|
| AQ-11 | Epic | Moyen | — | Audit & traçabilité | — | Haute (conformité cantonale) |
| AQ-22 | Story | Moyen | AQ-11 | Journal d'audit des actions | L (5–10 j) | Haute (obligation réglementaire 5 ans) |
| AQ-23 | Story | Bas | AQ-11 | Export données & statistiques | M (2–4 j) | Moyenne (transparence) |
| AQ-13 | Epic | Moyen | — | Optimisations | — | Haute (prod readiness) |
| AQ-84 | Tâche | Moyen | AQ-13 | Optimisation performance (requêtes, pagination, cache) | M (2–4 j) | Moyenne (à mesurer en prod) |
| AQ-85 | Tâche | Moyen | AQ-13 | Conformité accessibilité WCAG 2.1 AA | L (5–10 j) | **Haute** (obligation secteur public Suisse) |
| AQ-87 | Tâche | Moyen | AQ-13 | Documentation utilisateur intégrée | M (2–4 j) | Moyenne (onboarding terrain) |

\* Effort : S = ≤1j, M = 2–4j, L = 5–10j.

### Répartition par thème

| Thème | Tickets | Effort cumulé |
|---|---|---|
| Audit & Reporting | AQ-11, AQ-22, AQ-23 | L + M = ~7–14 j |
| Qualité & Prod readiness | AQ-13, AQ-84, AQ-85, AQ-87 | M + L + M = ~9–18 j |

**Total backlog restant : ~16–32 jours dev.**

### Points d'attention

- **Aucun ticket relatif à Limsophy réel** (intégration AAC Infotray) : le Mock LIMS v0.92 couvre tous les contrats API. Un futur epic "Intégration Limsophy réelle" sera créé **seulement quand** le contrat AAC sera finalisé. À signaler au PO lors du bilan v0.92.
- **Aucun ticket email réel** : Notifications v0.92 sont Mock (in-app + log). Migration vers vrai SMTP = epic "Production v1.0".
- **AQ-84 (perf)** doit idéalement être scopé **après mesure en prod** avec données réelles, pas avant.
- **AQ-85 (WCAG)** : obligation légale pour portail cantonal — ne peut pas être repoussé sine die.

---

## 3. Proposition v0.93 — "Production Readiness : Audit, Accessibilité & Documentation"

### Thème

Clôturer les 2 derniers epics transverses (AQ-11 + AQ-13) pour obtenir une application **prête pour la mise en production cantonale** :
- Traçabilité conforme (5 ans, obligation légale)
- Accessibilité WCAG 2.1 AA (obligation secteur public Suisse)
- Reporting minimal (exports administratifs)
- Documentation utilisateur (onboarding terrain)
- Optimisations performance basées sur l'utilisation réelle

Cohérence thématique forte : **tout ce qui reste** converge vers la prod-readiness. Pas de dilution.

### Scope proposé (5 stories)

| Ordre | Key | Titre | Effort | Justification priorité |
|---|---|---|---|---|
| 1 | AQ-85 | Conformité accessibilité WCAG 2.1 AA | L (5–10 j) | **Obligation légale cantonale** — bloquant pour passage en production officielle |
| 2 | AQ-22 | Journal d'audit des actions | L (5–10 j) | **Obligation réglementaire** (traçabilité 5 ans) — conformité protection des données publique |
| 3 | AQ-87 | Documentation utilisateur intégrée | M (2–4 j) | Aide contextuelle + tour guidé pour préleveurs terrain — réduit la charge support |
| 4 | AQ-23 | Export des données et statistiques | M (2–4 j) | Reporting CSV/Excel des mandats et résultats — transparence administrative |
| 5 | AQ-84 | Optimisation performance | M (2–4 j) | Pagination + index DB + cache — basé sur métriques v0.92 |

**Effort global estimé** : 19–30 jours dev (~3–5 semaines, sprint unique compact).

### Dépendances

```
AQ-85 (WCAG) ─── indépendant, audit + remédiation transverse
AQ-22 (audit) ─── indépendant, table d'audit + UI consultation
AQ-87 (doc) ─── indépendant, aide contextuelle
AQ-23 (export) ─── indépendant, endpoints + UI export
AQ-84 (perf) ─── dernier, basé sur métriques récoltées pendant v0.93
```

Aucune dépendance interne → parallélisation possible, faible risque bloquant.

### Risques

| Risque | Sévérité | Mitigation |
|---|---|---|
| WCAG audit externe révèle un gros écart | Moyen | Démarrer par un audit interne (axe-core / Lighthouse) pour chiffrer avant commit scope |
| Décision archi journal d'audit (event-sourcing vs table classique) | Moyen | Décision Team Lead avant sprint — recommandation : table `audit_log` classique + filtres (KISS) |
| Perf (AQ-84) sans métriques exploitables | Bas | Reporter AQ-84 si aucune donnée prod — acceptable |

### Points non couverts (assumés hors scope v0.93)

- **Intégration Limsophy réelle** (AAC Infotray) : attendre contrat signé
- **Notifications email réelles** (SMTP) : epic production v1.0
- **Audit sécurité externe** : jalon v1.0 séparé (pas un ticket actuel)

---

## 4. Reste post v0.93

Après v0.93, **0 ticket actif** sur le backlog Jira. La version pourra recevoir un cachet "v1.0 — Production Ready" sous réserve de :
- Audit sécurité externe (recommandé pour portail cantonal)
- Signature contrat AAC Infotray → ouvrir epic "Intégration Limsophy réelle"
- Signature infra email production → ouvrir epic "Notifications SMTP réel"
- Retours utilisateurs terrain v0.92 → éventuels nouveaux tickets

Le backlog PO redeviendra actif uniquement sur **déclencheurs externes** (utilisateurs, contrats, réglementation).

---

## 5. Recommandation finale

### Faut-il faire v0.93 maintenant, ou passer en QA finale directement ?

**Recommandation : faire v0.93 AVANT QA finale.**

Raisons :

1. **AQ-85 (WCAG)** est une **obligation légale** pour un portail cantonal suisse. Sans elle, la mise en production officielle est bloquée. Inutile de lancer une QA finale sur un périmètre qui va changer.
2. **AQ-22 (journal d'audit)** est une **exigence réglementaire** (traçabilité 5 ans, protection des données publiques). Idem — doit être en place avant recette officielle.
3. Le reste (AQ-84, AQ-23, AQ-87) est cohérent thématiquement avec la prod-readiness et tient dans le même sprint sans exploser le scope.
4. **Effort maîtrisé** (19–30 j) comparable aux sprints précédents, aucune dépendance externe bloquante.
5. Après v0.93, une **unique QA finale complète (L1→L4 + non-régression AQ-194)** couvrira un périmètre stable et livrable en v1.0.

### Alternative rejetée

Passer en QA finale maintenant puis faire v0.93 après : dédouble l'effort QA (double non-régression), retarde la v1.0, expose à des régressions WCAG/audit découvertes tardivement.

### Ordre d'exécution recommandé

1. **Kickoff v0.93** : créer la fixVersion `v0.93 - Production Readiness` dans Jira, transitionner les 7 tickets restants en `En cours` (Definition of Ready du projet).
2. **Sprint 1 v0.93** (2 sem) : AQ-85 (WCAG) + AQ-22 (audit) en parallèle — les 2 plus gros items.
3. **Sprint 2 v0.93** (1–2 sem) : AQ-87 (doc) + AQ-23 (export) + AQ-84 (perf).
4. **QA finale v0.93** : L1→L4 + non-régression AQ-194 complète.
5. **Release v1.0** : audit sécurité externe + déploiement production cantonale.

### Estimation globale

| Jalon | Stories | Effort dev | Durée |
|---|---|---|---|
| v0.93 Production Readiness | 5 | 19–30 j | ~3–5 sem |
| Audit sécurité externe (hors dev) | — | — | ~1 sem |
| **v1.0 Production** | — | — | **~4–6 sem** |

---

## 6. Synthèse exécutive

| Indicateur | Valeur |
|---|---|
| Tickets fermés pendant cet audit | **23** |
| Tickets actifs restants | **7** |
| Versions livrées à date | v0.1 → v0.92 (14 versions) |
| Bugs ouverts | 0 |
| Tickets `Highest`/`Haut` ouverts | 0 |
| Proposition unique v0.93 | 5 stories, 19–30 j, thème "Production Readiness" |
| Chemin vers v1.0 | v0.93 → audit sécurité → v1.0 |

**Le backlog est propre. Une dernière itération v0.93 cohérente clôture le projet côté fonctionnel.**

---

*Fin du document — à valider par le user avant kickoff v0.93 (création fixVersion + transition des 7 tickets en `En cours`).*

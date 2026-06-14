> ⚠️ **DOCUMENT ARCHIVÉ — OBSOLÈTE (audit F-040cc).**
> Planning initial « 4 releases » dépassé par la réalité (v0.94+ livrée). Référence
> `AquaPlan.sln` alors que le repo utilise `AquaPlan.slnx`, et renvoie vers Confluence
> externe. Conservé pour historique uniquement — ne pas s'y fier pour l'état courant.

# AquaPlan — Plan de développement

## Vue d'ensemble

Ce plan organise le développement d'AquaPlan en **4 releases** progressives, chacune livrant de la valeur utilisable. Les stories fonctionnelles sont documentées dans l'espace Confluence [Claude Coding](https://chfr.atlassian.net/wiki/spaces/CC/), sous la page "User Stories" (15 domaines, 70 stories). Les tickets seront créés dans le projet Jira [AQ](https://chfr.atlassian.net/jira/software/c/projects/AQ/summary).

## Référentiel Confluence — User Stories

| # | Domaine | Préfixe | Stories | Page Confluence |
|---|---|---|---|---|
| 0 | Setup technique & CI/CD | US-SETUP | 8 | [243302418](https://chfr.atlassian.net/wiki/spaces/CC/pages/243302418) |
| 1 | Authentification | US-AUTH | 2 | [242057784](https://chfr.atlassian.net/wiki/spaces/CC/pages/242057784) |
| 2 | Gestion des comptes utilisateurs | US-USER | 3 | [242155557](https://chfr.atlassian.net/wiki/spaces/CC/pages/242155557) |
| 3 | Permissions et rôles | US-PERM | 9 | [242417685](https://chfr.atlassian.net/wiki/spaces/CC/pages/242417685) |
| 4 | Création et gestion des mandats | US-MAN | 7 | [242417706](https://chfr.atlassian.net/wiki/spaces/CC/pages/242417706) |
| 5 | Consultation des mandats | US-CONS | 5 | [242057805](https://chfr.atlassian.net/wiki/spaces/CC/pages/242057805) |
| 6 | Plans de prélèvement | US-PLAN | 2 | [242155577](https://chfr.atlassian.net/wiki/spaces/CC/pages/242155577) |
| 7 | Traitement des mandats (prélèvements) | US-PREV | 10 | [242548737](https://chfr.atlassian.net/wiki/spaces/CC/pages/242548737) |
| 8 | Interfaces et résultats | US-INT | 4 | [242581506](https://chfr.atlassian.net/wiki/spaces/CC/pages/242581506) |
| 9 | Administration du système | US-ADMIN | 8 | [242614273](https://chfr.atlassian.net/wiki/spaces/CC/pages/242614273) |
| 10 | Tournées de prélèvement | US-TOUR | 3 | [242647042](https://chfr.atlassian.net/wiki/spaces/CC/pages/242647042) |
| 11 | Notifications | US-NOTIF | 3 | [242679809](https://chfr.atlassian.net/wiki/spaces/CC/pages/242679809) |
| 12 | Mode hors ligne | US-OFFL | 3 | [242712577](https://chfr.atlassian.net/wiki/spaces/CC/pages/242712577) |
| 13 | Tableau de bord | US-DASH | 1 | [242548758](https://chfr.atlassian.net/wiki/spaces/CC/pages/242548758) |
| 14 | Audit et traçabilité | US-AUDIT | 1 | [242745345](https://chfr.atlassian.net/wiki/spaces/CC/pages/242745345) |

**Total : 70 stories** réparties sur 15 domaines fonctionnels et techniques.

---

## Release 1 — Fondations & Données de référence
**Objectif** : Poser l'infrastructure technique et permettre la gestion des données de base.
**Durée estimée** : 4-6 sprints

### Epic 1.1 — Setup technique & CI/CD (Confluence: domaine 0)
| Story Confluence | Description |
|---|---|
| US-SETUP-001 | Initialiser la solution .NET (AquaPlan.sln + projets) |
| US-SETUP-002 | Configurer PostgreSQL + EF Core (DbContext, migrations, docker-compose) |
| US-SETUP-003 | Initialiser le projet Angular (Angular 19, Material, Bootstrap, i18n) |
| US-SETUP-004 | Configurer NSwag (auto-génération clients TypeScript) |
| US-SETUP-005 | Configurer l'authentification (JWT + OIDC + ASP.NET Identity) |
| US-SETUP-006 | Mettre en place le CI/CD (Bitbucket Pipelines) |
| US-SETUP-007 | Configurer le logging & télémétrie (Serilog, ngx-logger, OpenTelemetry) |
| US-SETUP-008 | Dockeriser l'application (Dockerfile multi-stage, docker-compose) |

### Epic 1.2 — Authentification & Utilisateurs (Confluence: domaines 1, 2, 3)
| Story Confluence | Description |
|---|---|
| US-AUTH-001..002 | Authentification (IdP, SSO, ex. EntraID) |
| US-USER-001..003 | Gestion des comptes utilisateurs |
| US-PERM-001..009 | Permissions et rôles |

### Epic 1.3 — Administration du système (Confluence: domaine 9)
| Story Confluence | Description |
|---|---|
| US-ADMIN-001..008 | Configuration système, paramètres, données de référence |

---

## Release 2 — Mandats, planification et prélèvements
**Objectif** : Permettre la création de mandats, la planification des tournées et l'exécution des prélèvements.
**Durée estimée** : 4-6 sprints

### Epic 2.1 — Création et gestion des mandats (Confluence: domaine 4)
| Story Confluence | Description |
|---|---|
| US-MAN-001..007 | Créer, modifier, valider, annuler des mandats d'analyse |

### Epic 2.2 — Plans de prélèvement & Tournées (Confluence: domaines 6, 10)
| Story Confluence | Description |
|---|---|
| US-PLAN-001..002 | Plans de prélèvement |
| US-TOUR-001..003 | Tournées de prélèvement |

### Epic 2.3 — Traitement des mandats / prélèvements (Confluence: domaine 7)
| Story Confluence | Description |
|---|---|
| US-PREV-001..010 | Saisie terrain, validation, prélèvements non planifiés |

---

## Release 3 — Intégration LIMS, résultats & consultation
**Objectif** : Connecter AquaPlan à Limsophy et permettre la consultation des résultats.
**Durée estimée** : 4-6 sprints

### Epic 3.1 — Intégration Limsophy (Confluence: domaine 8)
| Story Confluence | Description |
|---|---|
| US-INT-001..004 | Client Limsophy, envoi ordres, réception résultats, mapping |

### Epic 3.2 — Consultation des mandats & résultats (Confluence: domaine 5)
| Story Confluence | Description |
|---|---|
| US-CONS-001..005 | Consultation, recherche, filtrage, export des mandats et résultats |

### Epic 3.3 — Notifications (Confluence: domaine 11)
| Story Confluence | Description |
|---|---|
| US-NOTIF-001..003 | Alertes, notifications email/in-app |

---

## Release 4 — Tableaux de bord, audit & optimisations
**Objectif** : Tableaux de bord, rapports officiels, audit, mode offline.
**Durée estimée** : 3-4 sprints

### Epic 4.1 — Tableau de bord (Confluence: domaine 13)
| Story Confluence | Description |
|---|---|
| US-DASH-001 | Dashboard principal avec KPIs et vue d'ensemble |

### Epic 4.2 — Audit et traçabilité (Confluence: domaine 14)
| Story Confluence | Description |
|---|---|
| US-AUDIT-001 | Journal d'audit, traçabilité des actions |

### Epic 4.3 — Mode hors ligne (Confluence: domaine 12)
| Story Confluence | Description |
|---|---|
| US-OFFL-001..003 | Fonctionnement offline, synchronisation |

### Epic 4.4 — Optimisations
| Story | Description |
|---|---|
| Performance | Optimisation des requêtes, pagination, caching |
| Accessibilité | Conformité WCAG 2.1 AA (obligation administration publique) |
| Responsive / mobile | Optimisation de l'UI pour tablettes terrain |
| Documentation utilisateur | Guide utilisateur intégré dans l'application |

---

## Rôles utilisateurs

| Rôle | Description |
|---|---|
| Administrateur | Configuration système, gestion utilisateurs, paramètres |
| Officier cantonal | Créer des prescriptions, consulter tous les résultats, rapports |
| Préleveur | Exécuter les prélèvements terrain, saisir les données |
| Laborantin | Consulter les résultats, gérer le catalogue d'analyses |
| Lecteur | Consultation seule (résultats, rapports) |

---

## Références

| Ressource | Lien |
|---|---|
| Bitbucket repo | https://bitbucket.org/francisuster/aquaplan/src/Main/ |
| Confluence (User Stories) | https://chfr.atlassian.net/wiki/spaces/CC/pages/242319368 |
| Confluence (Architecture) | https://chfr.atlassian.net/wiki/spaces/CC/pages/242843658 |
| Jira (projet AQ) | https://chfr.atlassian.net/jira/software/c/projects/AQ/summary |
| LIMS | Limsophy (AAC Infotray) |

---

## Prochaines étapes

1. **Relecture de ce plan** par François
2. **Créer les Epics et Stories dans Jira AQ** à partir des stories Confluence
3. **Démarrer le Release 1** — Epic 1.1 (Setup technique) en priorité
4. **Raffiner les stories** du Release 2 en parallèle avec le PO

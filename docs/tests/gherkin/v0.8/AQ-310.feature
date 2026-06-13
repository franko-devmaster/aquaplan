Feature: AQ-310 Tuiles dashboard cliquables avec pre-filtres
  En tant qu'utilisateur du dashboard
  Je veux que toutes les tuiles soient cliquables avec pre-filtres
  Afin de naviguer en un clic vers ma tache

  Scenario: Navigation pre-filtree depuis chaque tuile
    Etant donne un utilisateur sur la page d'accueil
    Quand il clique sur la tuile Mandats a finaliser
    Alors le navigateur redirige vers /orders avec statuses=InProgress,Completed
    Et la liste des mandats est pre-filtree sur ces deux statuts
    Et la tuile Tournees planifiees navigue vers /sampling-rounds?status=Assigned
    Et la tuile Mandats completes navigue vers /orders?statuses=Transmitted,Done
    Et la tuile LDP a valider navigue vers /admin/validation-queue

  Scenario: Tuile Non-conformites stub inactif
    Etant donne un utilisateur sur la page d'accueil
    Quand il survole ou clique sur la tuile Non-conformites
    Alors le compteur affiche 0
    Et le curseur reste default pas de pointer
    Et aucune navigation ne se declenche
    Et aucune erreur console n'est generee

  Scenario: Tuile LDP a valider masquee pour non-Admin
    Etant donne un utilisateur sans role Admin
    Quand il charge le dashboard
    Alors la tuile LDP a valider est masquee ou non cliquable
    Et un acces direct a /admin/validation-queue est refuse par AuthorizeGuard

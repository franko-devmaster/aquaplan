Feature: AQ-371 Tournee InProgress verrouillee en modification pour mandataire

  Tant que la tournee est locked par le preleveur, toute ecriture sur
  la tournee ou ses mandats par un autre utilisateur (y compris Admin
  en modification normale) retourne 409 Conflict avec error="round.locked".
  Seul le preleveur proprietaire du lock peut ecrire. L endpoint
  force-unlock Admin fait exception (voir AQ-372).

  Scenario: Mandataire tente de modifier un mandat d une tournee verrouillee
    Etant donne une tournee T1 en InProgress verrouillee par le preleveur U1 (nom "Jean Dupont")
    Et un Requerant U2 mandataire du mandat M1 appartenant a T1
    Quand U2 envoie PUT /api/orders/{M1} avec des modifications
    Alors la reponse est 409 Conflict
    Et le body contient error="round.locked", lockedByName, lockedAt
    Et aucune modification n est persistee

  Scenario: Operations d ajout/retrait de mandat bloquees quand locked
    Etant donne une tournee T1 verrouillee par U1
    Quand un mandataire tente POST /api/sampling-rounds/{T1}/orders/{orderId}
    Alors la reponse est 409 Conflict avec error="round.locked"
    Et les operations de lecture (GET) restent autorisees

  Scenario: Permission - le preleveur proprietaire peut toujours ecrire
    Etant donne une tournee T1 verrouillee par le preleveur U1
    Quand U1 envoie PUT /api/samplings/{id} pour saisir un prelevement
    Alors la reponse est 200 OK
    Et meme un Admin qui tente PUT /api/orders/{M1} recoit 409 Conflict (cohesion stricte)

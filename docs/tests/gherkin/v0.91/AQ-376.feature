Feature: AQ-376 Sync queue au retour en ligne - gestion 409 et retry 5xx

  Au retour en ligne (evenement navigator 'online'), le SyncService
  rejoue en FIFO les actions en attente via l API. Gestion :
  2xx -> retire l action, 409 round.unlocked -> purge queue + snapshot,
  5xx -> retry exponentiel (2s/5s/15s puis abandon), 4xx hors 409 ->
  retire + toast erreur, 401 -> redirection login sans purge.

  Scenario: Replay reussi de 3 actions en FIFO
    Etant donne le preleveur a 3 actions en queue [A, B, C]
    Quand le reseau revient et SyncService.flushQueue est declenche
    Alors PUT /api/samplings/A est envoye puis 200, retire de la queue
    Puis B puis C dans le meme ordre avec 200
    Et un toast discret "3 actions synchronisees" s affiche
    Et getPendingActions retourne [] a la fin

  Scenario: 409 round.unlocked purge la queue et le snapshot
    Etant donne une tournee force-unlock par Admin pendant la session offline
    Et le preleveur a 5 actions en queue
    Quand la premiere action est rejouee
    Alors l API retourne 409 Conflict avec error="round.unlocked"
    Et SyncService purge integralement la queue
    Et offlineStorage.clearSnapshot(roundId) est appele
    Et un toast rouge persistant informe "Tournee reinitialisee par un administrateur"

  Scenario: Retry exponentiel sur 5xx puis abandon
    Etant donne une action en queue
    Quand le serveur renvoie 503 a chaque tentative
    Alors SyncService retry apres 2s, puis 5s, puis 15s
    Et abandonne avec toast "Erreur serveur, reessayez plus tard"
    Et l action reste dans la queue pour la prochaine occurrence online
    Et en cas de 401, l utilisateur est redirige vers login sans purger la queue

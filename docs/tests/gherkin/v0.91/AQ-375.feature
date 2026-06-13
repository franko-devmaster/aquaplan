Feature: AQ-375 Stockage IndexedDB - snapshot actif et file d actions

  Le service OfflineStorageService utilise IndexedDB (via la lib idb)
  pour persister le snapshot de tournee actif et la file FIFO des
  actions en attente. Les donnees survivent a la fermeture de
  l onglet et sont purgees au logout.

  Scenario: Sauvegarde et relecture du snapshot actif
    Etant donne le preleveur a demarre sa tournee T1 en ligne
    Et OfflineStorageService.saveSnapshot(snapshot) a ete appele
    Quand on appelle OfflineStorageService.getSnapshot(T1.Id)
    Alors les memes donnees sont retournees (roundId, orders, samplingLocations, ...)
    Et les donnees survivent a la fermeture/reouverture de l onglet

  Scenario: File FIFO d actions offline
    Etant donne le preleveur a saisi offline 3 prelevements A, B, C
    Quand on appelle queueAction 3 fois puis getPendingActions
    Alors les 3 actions sont retournees dans l ordre d insertion
    Et removeAction(1) retire la premiere action et getPendingActions retourne [2, 3]

  Scenario: Purge au logout et isolation entre sessions
    Etant donne l utilisateur U1 se deconnecte
    Quand auth.service.logout declenche offlineStorage.clearAll
    Alors les object stores active-round et pending-actions sont vides
    Et un autre utilisateur U2 qui se connecte ne voit aucune donnee de U1

Feature: AQ-337 Suppression totale des champs latitude/longitude

  Scenario: Happy path - suppression complete et migration DB
    Given une base avec colonnes location_lat et location_lng dans samplings et sampling_locations
    When la migration RemoveLatLng est appliquee
    Then les colonnes location_lat et location_lng n'existent plus dans les tables concernees
    And aucune regression n'est introduite sur les requetes existantes
    And les tests xUnit backend passent

  Scenario: Edge case - formulaire UI
    Given un preleveur ouvre sampling-form-dialog
    When le formulaire s'affiche
    Then aucun champ Latitude ou Longitude n'est present
    And aucun bouton Obtenir ma position n'apparait
    And le meme nettoyage s'applique a sampling-location-form et a order-detail

  Scenario: Permission - DTOs et API
    Given un appel forge POST /api/samplings avec un body contenant locationLat=46.8 et locationLng=7.15
    When l'API traite la requete
    Then les proprietes sont ignorees (absentes du DTO) ou rejetees par validation
    And la ressource est creee sans champ geoloc
    And les cles i18n samplings.location.{lat,lng} sont absentes des 3 fichiers i18n

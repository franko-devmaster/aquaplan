Feature: AQ-342 Scan code-barre via camera (BarcodeDetector + ZXing fallback)

  Scenario: Happy path - scan natif Chrome Android
    Given un preleveur sur Chrome Android avec BarcodeDetector API disponible
    When il clique sur l'icone camera a cote du champ Bouteille PET chimie
    Then un dialog plein ecran s'ouvre avec le flux video de la camera arriere
    And quand le preleveur cadre un code-barres EAN-13 ou Code-128, le code est detecte en moins de 2s
    And le dialog se ferme automatiquement
    And la valeur est injectee dans le champ et BarcodeScannedAt = now()

  Scenario: Edge case - iPad Safari fallback ZXing
    Given un preleveur sur iPad Safari ou BarcodeDetector n'existe pas
    When il clique sur l'icone camera
    Then le composant feature-detect retombe sur @zxing/browser
    And le scan fonctionne avec la meme UX (dialog plein ecran, detection auto, close on detect)
    And la performance est acceptable (moins de 5s en conditions normales)

  Scenario: Permission - fallback manuel
    Given un preleveur refuse la permission camera
    When il clique sur l'icone camera
    Then un message clair s'affiche "Permission camera refusee. Saisissez le code manuellement."
    And le dialog plein ecran ne s'ouvre pas
    And le champ de saisie manuelle reste focusable
    And le bouton camera peut etre reessaye apres reset permission navigateur

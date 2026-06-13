Feature: AQ-374 Service Worker Angular + manifest PWA + cache ngsw

  L application doit servir un Service Worker et un manifest PWA
  valides afin d etre installable et de fonctionner offline apres
  le premier chargement. Strategies de cache definies dans
  ngsw-config.json.

  Scenario: Service Worker servi avec Content-Type application/javascript
    Etant donne l application Angular est deployee derriere le reverse proxy
    Quand on appelle GET /ngsw-worker.js
    Alors la reponse est 200 OK
    Et le header Content-Type est application/javascript

  Scenario: Manifest PWA accessible et valide
    Etant donne l application est deployee
    Quand on appelle GET /manifest.webmanifest
    Alors la reponse est 200 OK
    Et le body est un JSON valide contenant name, short_name, theme_color, icons

  Scenario: L2 - verification d accessibilite du ngsw.json (config runtime)
    Etant donne le Service Worker est enregistre
    Quand on appelle GET /ngsw.json
    Alors la reponse est 200 OK
    Et le body est un JSON valide decrivant les assetGroups et dataGroups

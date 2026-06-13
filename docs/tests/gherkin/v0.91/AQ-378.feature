Feature: AQ-378 Header - indicateurs "Hors ligne" et badge actions en attente

  Deux badges complementaires dans le header :
  - rouge "Hors ligne" si navigator.onLine === false
  - orange "N en attente" si syncService.pendingCount > 0.
  Ils peuvent apparaitre simultanement. Tooltips au hover.
  Aucune restriction metier.

  Scenario: Online sans queue - aucun badge
    Etant donne l utilisateur est en ligne et sa queue est vide
    Quand il regarde le header
    Alors aucun badge offline n est affiche
    Et aucun badge "en attente" n est affiche

  Scenario: Offline avec queue - deux badges visibles
    Etant donne le preleveur passe en mode avion
    Et sa queue contient 4 actions
    Alors le header affiche un badge rouge "Hors ligne" avec icone cloud_off
    Et un badge orange "4 en attente" avec icone sync_problem
    Et au hover le tooltip explique l etat
    Quand le reseau revient et les 4 actions sont rejouees
    Alors les deux badges disparaissent

  Scenario: Responsive mobile - badges compactes et accessibles
    Etant donne un viewport inferieur a 768px
    Quand les badges sont affiches
    Alors ils sont compactes (icones seules avec nombre en overlay)
    Et aria-label="Hors ligne" / aria-label="4 actions en attente" sont presents
    Et le contraste WCAG AA est respecte

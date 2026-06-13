Feature: AQ-379 Footer application avec version GitVersion et auteur

  Un footer non-sticky, discret (font 11px, couleur gris clair),
  affiche la version AquaPlan (calculee par GitVersion) et le nom
  du developpeur. Fallback hardcode si script pre-build echoue.
  i18n pour "Developpe par" / "Entwickelt von" / "Developed by".

  Scenario: Footer present avec version courante
    Etant donne l application est buildee avec GitVersion retournant SemVer "0.91.0"
    Quand un utilisateur navigue sur n importe quelle page
    Alors un footer est visible en bas du contenu principal (non-sticky)
    Et il affiche "AquaPlan v0.91.0 — Developpe par Francois Charriere"
    Et le style est discret (11px, gris clair, padding reduit, centre)

  Scenario: Fallback si la version n a pas ete generee
    Etant donne le script generate-version.js a echoue
    Quand l app se charge
    Alors le footer affiche la version fallback "v0.91 — Developpe par Francois Charriere"
    Et aucune erreur JS n apparait en console

  Scenario: i18n - texte traduit en fr/de/en et footer visible sur /login
    Etant donne l app change de langue (fr, de, en)
    Quand le footer est rendu
    Alors "Developpe par" est traduit correctement ("Developpe par" / "Entwickelt von" / "Developed by")
    Et le nom "Francois Charriere" reste inchange
    Et le footer est present y compris sur /login (non authentifie)

Feature: AQ-312 Bordure grise fine bouton utilisateur header
  En tant qu'utilisateur de l'application
  Je veux une bordure grise fine sur le bouton utilisateur du header
  Afin qu'il soit mieux delimite visuellement

  Scenario: Bordure visible sur fond primary
    Etant donne un utilisateur authentifie affichant le header
    Quand le bouton utilisateur est rendu
    Alors il porte un style border 1px solid rgba 0 0 0 0.2
    Et la bordure est visible sur fond primary

  Scenario: Hover et focus preserves
    Etant donne le bouton utilisateur affiche avec sa bordure
    Quand l'utilisateur le survole ou lui donne le focus clavier
    Alors l'etat hover ou focus reste visible et ne fait pas disparaitre la bordure
    Et l'anneau de focus accessibilite reste present

  Scenario: Coherence multi-langue et multi-role
    Etant donne un utilisateur dans n'importe quelle langue fr, de, en et n'importe quel role
    Quand le header est affiche desktop et mobile
    Alors la bordure apparait identique sur tous les breakpoints
    Et aucune regression visuelle n'est introduite sur les autres boutons du header

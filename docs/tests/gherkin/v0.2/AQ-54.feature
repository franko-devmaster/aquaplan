Feature: Affectation et retrait de rôles

	Scenario: L'administrateur affecte un rôle à un utilisateur
		Given L'utilisateur est authentifié en tant qu'administrateur
			And Un utilisateur sans rôle est affiché
		When L'administrateur affecte le rôle Préleveur à l'utilisateur
		Then Le rôle est attribué avec succès
			And Un message de confirmation est affiché

	Scenario: L'administrateur retire un rôle
		Given L'utilisateur est authentifié en tant qu'administrateur
			And Un utilisateur avec un rôle attribué est affiché
		When L'administrateur retire le rôle de l'utilisateur
		Then Le rôle est retiré avec succès
			And Les permissions associées au rôle ne sont plus accessibles

	Scenario: Le changement de rôle est tracé dans l'audit trail
		Given Un utilisateur a changé de rôle
		When L'administrateur consulte l'audit trail du changement de rôle
		Then Le changement est enregistré avec la date et l'heure
			And L'administrateur qui a effectué le changement est identifié
			And L'ancien rôle et le nouveau rôle sont tracés

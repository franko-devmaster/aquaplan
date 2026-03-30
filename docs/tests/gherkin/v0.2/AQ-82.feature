Feature: Modification d'un compte utilisateur

	Scenario: L'administrateur modifie les informations d'un utilisateur
		Given L'utilisateur est authentifié en tant qu'administrateur
			And Un utilisateur existant est affiché
		When L'administrateur modifie le prénom de l'utilisateur
		Then Les modifications sont sauvegardées
			And Un message de confirmation est affiché

	Scenario: Le changement de rôle notifie l'utilisateur par email
		Given L'utilisateur est authentifié en tant qu'administrateur
			And Un utilisateur existant est affiché
		When L'administrateur change le rôle de l'utilisateur
		Then Le rôle est mis à jour
			And Un email de notification est envoyé à l'utilisateur
			And L'email mentionne le nouveau rôle attribué

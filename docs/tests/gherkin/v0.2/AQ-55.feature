Feature: Visibilité restreinte aux LDP du réseau

	Scenario: Un utilisateur ne voit que les LDP de son réseau de distribution
		Given L'utilisateur est authentifié avec le rôle Requérant
			And L'utilisateur est associé au réseau de distribution "Réseau Nord"
		When L'utilisateur accède à la liste des points de prélèvement
		Then Seuls les LDP du réseau "Réseau Nord" sont affichés
			And Les LDP d'autres réseaux ne sont pas visibles

	Scenario: Un administrateur voit tous les LDP
		Given L'utilisateur est authentifié avec le rôle Administrateur
		When L'utilisateur accède à la liste des points de prélèvement
		Then Tous les LDP de tous les réseaux sont affichés

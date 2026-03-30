Feature: Initialiser le projet Angular

	Scenario: L'application Angular démarre sur localhost:4200
		Given Le projet Angular AquaPlan.Web/ClientApp est configuré
		When L'utilisateur exécute ng serve
		Then L'application démarre sans erreur
			And L'application est accessible sur http://localhost:4200

	Scenario: Angular Material et Bootstrap sont configurés
		Given L'application Angular est en cours d'exécution
		When L'utilisateur consulte la page d'accueil
		Then Les styles Angular Material sont appliqués
			And Les styles Bootstrap sont appliqués

	Scenario: La traduction fonctionne pour le français et l'allemand
		Given L'application Angular est en cours d'exécution
			And L'utilisateur accède à la page d'accueil
		When L'utilisateur change la langue en français
		Then Le contenu s'affiche en français
		When L'utilisateur change la langue en allemand
		Then Le contenu s'affiche en allemand

	Scenario: Le layout principal s'affiche avec les composants attendus
		Given L'application Angular est en cours d'exécution
		When L'utilisateur accède à la page d'accueil
		Then Le header est affiché
			And La barre latérale est affichée
			And La zone de contenu est affichée

Feature: Containeriser l'application

	Scenario: docker-compose démarre tous les services
		Given Docker et docker-compose sont installés
			And Le fichier docker-compose.yml existe
		When L'utilisateur exécute docker-compose up
		Then Le conteneur PostgreSQL démarre
			And Le conteneur API démarre
			And Le conteneur frontend démarre
			And Le conteneur Redis démarre

	Scenario: L'API est accessible sur le port 5000
		Given docker-compose est en cours d'exécution
		When L'utilisateur accède à http://localhost:5000
		Then L'API répond avec un code 200
			And Swagger UI est accessible

	Scenario: Le frontend est accessible sur le port 80
		Given docker-compose est en cours d'exécution
		When L'utilisateur accède à http://localhost:80
		Then L'application Angular démarre
			And La page d'accueil s'affiche

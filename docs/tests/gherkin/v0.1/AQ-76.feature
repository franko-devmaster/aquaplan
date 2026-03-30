Feature: Configurer le logging et la télémétrie

	Scenario: Serilog écrit les logs en console et fichier
		Given L'API AquaPlan est en cours d'exécution
			And Serilog est configuré avec des sinks console et fichier
		When L'API génère un événement de log
		Then Le log est écrit en console
			And Le log est écrit dans le fichier journal

	Scenario: Chaque requête a un Correlation ID unique
		Given L'API AquaPlan est en cours d'exécution
		When L'utilisateur envoie une requête HTTP
		Then Un Correlation ID unique est généré
			And Le Correlation ID est inclus dans les logs de la requête

	Scenario: Les traces OpenTelemetry sont générées
		Given L'API AquaPlan est configurée avec OpenTelemetry
		When L'API traite une requête
		Then Une trace OpenTelemetry est créée
			And La trace contient les spans pour chaque étape du traitement

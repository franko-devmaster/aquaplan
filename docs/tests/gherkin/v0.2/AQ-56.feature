Feature: Accès mandataire limité à ses distributeurs

	Scenario: Le requérant ne peut créer des mandats que pour ses distributeurs
		Given L'utilisateur est authentifié avec le rôle Requérant
			And L'utilisateur est associé à la commune "Fribourg"
			And La page de création de mandat est affichée
		When L'utilisateur sélectionne une commune pour créer un mandat
		Then Seule la commune "Fribourg" est disponible
			And Les autres communes ne sont pas sélectionnables

	Scenario: La validation se fait côté serveur
		Given Un utilisateur tente de créer un mandat pour une commune non autorisée
		When L'utilisateur envoie une requête POST via l'API
		Then L'API rejette la demande
			And Un message d'erreur indique que la commune n'est pas autorisée
